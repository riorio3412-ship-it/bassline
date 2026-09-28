# TrialReforge — the 심판 and the 은판 deck (final spec v2)

Status: final spec v2, 2026-09-27 (v1 revised per the first-time-player / veteran-reviewer critique; §0.1 lists every change). Owner: workflow `trial-reforge`. Supersedes v1, the three concepts (deck-duel, jury-sway, tactile-deck) and the old trial-fun designs.
Reads: `MurderFoundation.md` (CaseFile, layers, exposing paths), `CaseTruthApi.md` (TrialPack: presented story, lies, fallbacks, rule shields, foreshadow), `MysteryTrickStudy.md` (acts, Memory Stage, stress-free deduction, §3.16 「열한 시의 외침」), `CharacterBible.md` (lying tells), playtest triage F, the trial-fun audits.
Code verified today (23:10): `Sim/Murder/CaseApi.cs` exists with `TrialPack`, `ForTarget`, `Chapter`, `SchemeOf`, `PlannedScapegoat`, `BeatsSeenBy`, `Narrative` (shape per `CaseTruthApi.md` v1); `GameState.Mur` is a `MurderWorld` (`Cases`, `Beats`, `Schemes`, `Favours`, `Novelty`) but nothing populates `Mur.Cases` yet and there is no `CaseApi.Of` (CaseFile accessor); `MurderModel.cs` defines `CaseFile`, `KeyClue.Fake/Origin`, `OpenQuestion` + `TheorySeed`, `Fragment`, `NoveltyMemory`; there is no `Replay.StateAt` (`Replay` has `Record/BuildSegments/Script`), while `ReplayStage` has `StateOf(id, at)`, `Seek`, `Advance` and a private `Collapse` through `PhysicalRagdoll`; `Game/Cinema/CaseRecap.cs` already plays 「그날 밤의 재구성」 from the recording with an alive/death probe; `ActorRig.SetDarkFace(DarkFace{HollowGrin,CorneredStare,VeiledSmirk}, intensity, hold)`; `CourtroomView.Watch/Stare/Blink/SetTension`; `MusicDirector.SetState/SetIntensity/Stinger`; `CineSolver.Place/Clear/PropsClear/KeepIn`, `ShotKind.Thing`; `Profile.Assist` (0–2) and `Profile.Runs`; `Settings.SaveDir`; the Evidence toast at `Session.cs:326`; `Cases.HouseConfirm` at `Cases.cs:298`. No deck, debate or plate code exists yet.

---

## 0. The whole design on one screen

**Investigation.** When the House confirms a death, the case gets **ten photographs, 은판** (8–12 when the case is thin; 10 in the large majority). Some show what they seem to show (**실상**); four to six are real things whose first reading is wrong (**허상**: the culprit's staging, an innocent coincidence, someone's unrelated secret, a mistaken witness). Each plate is a real render of the thing as it was found — the broken teacup in shards on the parquet where it lay. Nothing else is evidence; everything else people say is conversation (동선, 인물). During the investigation the notebook only counts plates found ("은판 6/10") and says once: "다 못 찾아도 심판에서 풀 수 있다." The House engraves 2–3 **수수께끼** (riddles) at confirm, and a single "다음 —" line always says where to look next.

**The 심판.** Yusti gathers everyone and rings the bell. He states the count ("은판은 열 장입니다. 그중 다섯 장은 보이는 대로가 아닙니다."); plates other residents found are handed to 민혁 in one gesture. **One riddle at a time** is lit on the clock floor. For it, residents propose their own theories (**가설**) from what they saw, heard, fear and are; the culprit backs whichever wrong one protects them. The director focuses one theory ("지금 살펴볼 가설"); each theory makes one checkable claim. The player tests it with four plainly labelled actions — **사진 내밀기** (맞대기), **어떻게 아는지 묻기** (캐묻기), **재현해 보기** (재현), **더 듣기**. Theories collapse, evolve, or settle; a settled answer becomes a brass plaque and a line in the "정해진 것" list, and the riddle's reframe opens the next one. At the turning point the player says **그렇다면…**, and the Memory Stage **demonstrates the whole crime** with the real people and props while the culprit listens — a shadow in place of the killer — and the name plates of those who could not have done it fall. Then the player names someone (**지목**: one name, one plate). Only now the accused's composure candles are lit, and the duel starts: prepared lies, retreats that concede a little, a pushed scapegoat, twisted testimony, attacks on 민혁, Yusti's rules used as a shield — answered through different actions until proof sits on all three knots (기회 · 수단 · 거짓). The last candle goes out on the decisive, NPC-independent plate; the break is quiet and gothic. Then 「그날 밤의 재구성」 plays by itself, 민혁 reads the plain summary, the room votes with stones, Yusti gives the verdict, the culprit confesses **at their stand, alive**, 민혁 says his one line, the execution is brief and implied, the aftermath, the 현상실.

**Player-facing words.** Ch.1 teaches at most eight: 은판 (first shown as "은판 사진"), 수수께끼, 가설, 맞대기, 캐묻기, 재현, 그렇다면…, 지목. The action buttons use plain phrases; the terms appear on the one-time teaching card and in the backlog. Stamps are plain: **◉ 사실** (a plate whose meaning held) and **✕ 뒤집힘** (a plate whose first reading was wrong; its back is a **드러난 사실**). 실상/허상 are design words (enums, docs), not taught. From Ch.2: 추리판, 추리 도장 (notebook), 덮어 두기/밝히기, 누가 날 봤나.

| Word | Meaning |
|---|---|
| 은판 | a photograph-card of a thing as found; ◉ 사실 · ✕ 뒤집힘 |
| 수수께끼 | the current open question, engraved by the House |
| 가설 | a resident's theory about it (one checkable claim) |
| 맞대기 | "사진 내밀기": lay a plate against the focused 가설 |
| 캐묻기 | "어떻게 아는지 묻기": ask the holder how they know; on a rule, Yusti reads it |
| 재현 | "재현해 보기": play the 가설 (or the truth) on the Memory Stage |
| 그렇다면… | choose the inference that follows from what is settled |
| 지목 | name one person and lay one plate |

**What dies:** the fixed topic conveyor, quiet candle inquiries, CrossLedger, EngravedQuestion "which room", ThreadBoard, the 7-question RQ quiz, 36-card pickers with pages, witness sheets as cards, the gold ◆ and the 기타 list as ammunition, `claim:cN` hearsay cards, `T.Influence`, the hidden-suspicion HUD, every `IsCulprit` speaking weight, forced auto-advance, card-against-card "반대, 반대", the wobble state, the three-knot 지목 quiz, the 되짚기 placement quiz, riddle-hopping, hidden trust thresholds, and candles on anyone but the accused.

### 0.1 What changed from v1 (critique → where)

| Critique | Change | Section |
|---|---|---|
| A1 execution before confession | verdict → confession at the stand (alive) → 마지막 한마디 → brief execution → aftermath → 현상실 | §6.10 |
| A2 example breaks deck rules; A7 alibi plate belongs only to the culprit | new flagship example with a group-alibi FalseAlibi plate flipped by a revealed fact; alibi pairing rule | §2.2, §7 |
| A3 fakes over-point at the scapegoat | ≤2 fakes point at one person | §2.2, §2.4 |
| A4 fakes readable by wording | fixed face grammar per plate kind, banned hedge words, parity lint | §2.8, §2.4 |
| A5 candles as a lie detector | candles only on the accused after 지목; innocents get face/posture only | §6.6, §6.9 |
| A6 flipped plate in a knot vs rule 3 | a flipped plate's back is a 드러난 사실 and counts as proof | §1.3, §6.5 |
| B1 wall of text in 서막 | 2 readable beats; one riddle lit; new count line; first-time inner line | §6.1 |
| B2 ~240 combinations | one focused slide + ≤2 chips; one claim per slide; four plain buttons | §6.2, §6.3 |
| B3 riddle-hopping | one active riddle; 미뤄 두기 after two misses | §6.2 |
| B4 plaques forgotten | "정해진 것" HUD list (≤5 lines) | §6.8, §6.15 |
| B5 지목 quiz; B6 되짚기 quiz; F1 DR endgame order | 지목 = one name + one plate, knots fill in the duel; the 2막 demonstration is the reconstruction; 4막 = film + summary → vote | §6.7, §6.9, §6.10 |
| B7/G11 K8 guess | deterministic from 민혁's 동선, uncounted, Ch.2+ | §6.3 |
| B8 Borrowed gating; B9 late-plate cost; B10 completion anxiety | hand-over at the end of the investigation; free on 쉬움/보통; the "다 못 찾아도" line; `Next` names people | §2.5, §2.7 |
| B11 추리판 anxiety; B12 term budget | 추리판 from Ch.2 (쉬움 gets pair proposals); ≤8 Ch.1 terms | §4, §5 |
| C1 duel as "반대, 반대" | ≤2 consecutive 맞대기; ≥3 verbs in the duel | §6.6 |
| C2 padded cadence | K1/K8/K11/K12/K13 and 듣기-only floors excluded; ≥80% meaningful decisions | §6.1, §10 |
| C3 predictable theory families | ≥80% provenance; "sharp" holds the truth ≤50%; ≥⅓ riddles settle through an evolved wrong theory | §6.4 |
| C4/C5 structural tells; fixed skeleton | anti-meta gates; act-shape variants | §6.13 |
| C6 low stakes | the room strip moves on every miss and flip; 어려움 keeps the fake-as-proof cost | §6.8, §6.11 |
| C7 variety | 재현 by axis (sequence, route, sight, reach, access, timing) | §6.7 |
| D1 filler fakes | every fake needs a user (a theory holder or the culprit's lie); gate 100% | §2.2, §6.4 |
| D2 wobble humiliation | cut; plain refusal that also answers partner-dependent true plates | §6.5 |
| D3–D5 | 비밀 never the only fit; detail crops for fakes too; fake count public only from the 서막 and varying 4–6 | §2.2, §3, §4 |
| E1–E5 photos | two-frame M cards by geometry; lighter subject mask; inspect L in full colour; veiled body thumbnails; corrected witness quote; flash in the dark | §3, §4 |
| F2 grin culprit | quiet break by default; `HollowGrin` ≤1 per loop | §6.9 |
| F3 three-witness shield | not the Ch.1 showcase; y6, house-dark, night-lock are | §6.6, §6.12 |
| F4 chandelier / candle-thread tableau | flagship example is 「열한 시의 외침」 (the clockwork bird); no chandelier in Ch.1 | §7, §11 |
| F5 DR2 ch.1 echo | riddle-set lint | §1.12, §2.6 |
| F6 AA interjection | no voiced shout or caption burst on 맞대기; the brass clack only | §6.14 |

---

## 1. Binding rules

1. **심판**, never 재판/학급재판, in player text. Yusti only announces, runs procedure, reads rules verbatim and gives the verdict; he never comments, hints or lies. 김민혁 and Yusti are never culprits.
2. **The debate is people's reasoning, doubts, emotions and lies.** Plates are tools to test theories; a plate is never "the content" of a round. No card-vs-card loops.
3. **Only proof collapses a 가설 or fills a knot.** Proof is a true plate (it is stamped ◉ 사실 when it lands; a true plate whose `Needs` are unmet is not yet proof), the back of a ✕ 뒤집힘 plate (a 드러난 사실), a plaque, or a feasible/infeasible 재현. A 허상 laid as proof is flipped at once by whoever can; if nobody can yet, it is refused plainly with no state change. Fakes are true photographs with a false first reading, never fabricated (H6), each with a recorded flip route.
4. **The culprit fights with a plan** (TrialPack: story, lies with a budget, fallbacks, scapegoat, rule shields) and cracks gradually. Innocents defend themselves with the same moves, truthfully. No speaking, lean or vote weight reads `IsCulprit`.
5. **Fair play:** the aha is exposed by ≥2 true plates with disjoint roots, ≥1 of them body/scene/object/record/experiment. NPCs never break the aha. Nothing is invented after the fact.
6. **Stress-free:** one named current riddle at every moment; no timers by default; no pixel hunts; misses always give a written reason and cost something recoverable; never a dead end; plain Korean, plain times ("밤 10시 조금 넘어"); a plain case summary before the vote.
7. **Cadence:** from the first floor to the break, a counted decision every ≤4 readable beats (mean ≤3.0). Uncounted: riddle order (K1), 누가 날 봤나 (K8), the vote (K11), the last word (K12), 미뤄 두기 (K13) and floors where 더 듣기 is the only real option (the director never opens those). ≥80% of counted decisions have options leading to different states and need case knowledge. ≤3 spoken agreement lines per 심판 (agreement is bodies and gaze).
8. **Recaps show the living alive.** Every Memory Stage sketch, demonstration, 「그날 밤의 재구성」 and confession frame poses the victim alive and moving until their recorded (or, in a hypothesis, hypothesised) death tick, then collapses them physically (ragdoll). Never a pre-posed corpse. Photos never use a replay pose.
9. **Danganronpa as structure only.** No moving/scrolling statements, no aiming, no timed weak points, no bullets, no letter puzzles, no rebuttal duels, no panic crowd-talk, no comic-panel or placement closing, no shattering-glass refutation, no mascot, no BREAK hologram glitch, no voiced interjection or caption burst on laying a plate. Our surface is photographic: lies **burn** (nitrate blister), truth **develops** (silver to colour).
10. **The vote is earned from public facts** and is visible before it happens: the room strip shows each juror's current reading. Hidden suspicion only seeds a resident's first public stance and breaks ties (≤0.2). Ballots are anonymous; no grudges from ballots.
11. **Determinism:** deck, riddles, theories, counters, act shape and votes are deterministic per seed (no new RNG streams; `MurderHash.U01` or ordinal ranking). Saves round-trip IDENTICAL.
12. **Never reproduce the DR2 ch.1 set** (party + blackout + glow-marked weapon + body under a table + tablecloth) in examples, templates or tests; and no riddle set may combine a table-position question, a blackout moment and the three theory families "hid in fear / dropped something / moved after death" (lint in `debatefun`).
13. **Honest deck grammar:** a plate's title, face, photo treatment, detail crop and card layout never depend on whether it is true (§2.8, §3). The only thing that reveals a plate's nature is play.
14. **Ch.1 teaches ≤8 new terms** (§0); everything else in Ch.1 is plain words or 3D.

---

## 2. The 은판 deck (kernel — STAGE DECK)

### 2.1 Record (`Sim/Trial/Deck/DeckModel.cs`; saved in `GameState.Decks`, null until the first freeze; enums append-only once shipped)

```csharp
public enum PlateKind  { Body, Object, Trace, Fixture, Door, Record, Witness, Memory }
public enum PlateRole  { Body, Hinge, Confirm, Seam, Link, Clear,                          // true
                         Staged, Frame, FalseAlibi, Cover, Coincidence, Secret, Mistaken }  // fake
public enum PlateState { Unfound, Found, Borrowed, Late, Shown, Sealed, Flipped }          // Late = from the House's discovery photo

[Serializable] public sealed class CaseDeck {
  public string Incident; public int Loop, Chapter; public double Frozen; public long FrozenSeq;
  public string Source;                        // "case" | "pack" | "legacy" — which feeders contributed (comma list)
  public int FakeTarget;                       // 4 | 5 | 6, chosen at freeze by MurderHash (25/50/25) before filling
  public List<Plate> Plates = new List<Plate>();          // sorted by N
  public List<DeckClaim> Claims = new List<DeckClaim>();  // the presented story, frozen (stable ids "c1"…)
  public List<DeckMystery> Mysteries = new List<DeckMystery>(); // 2–3 riddles engraved at confirm (§2.6)
  public int TrueCount, FakeCount;             // FakeCount is public from the 서막; which is which is hidden
  public List<string> Log = new List<string>(); // why each slot was filled or left empty (deckdump)
}
[Serializable] public sealed class Plate {
  public string Id;            // "pl:<incident>:<k>", k = stable candidate ordinal
  public int N;                // 1..12 display number and hotkey (1–0); body is 1, the rest by MurderHash order — never by truth
  public string Root;          // Evidences.RootKey form: body:/item:/trace:/furn:/door:/talk:<witness>:<factKey>/mem:<beatId>
  public string Record;        // Record plates: the one ledger/log entry key it shows ("lock:44", "loan:it12", "clock:17")
  public PlateKind Kind; public PlateRole Role;
  public bool True;            // HIDDEN: read only by Sim/Trial resolution, votes, 현상실 and tests (grep gate: never in Game/)
  public string Origin;        // fakes, shown after the flip: 위장 | 감싸기 | 착각 | 우연 | 비밀 | 피해자의 계획
  public string Title;         // ≤12 chars, names the object or "<name>가 본 것/들은 것" (§2.8)
  public string Face;          // one plain sentence of observable features, fixed pattern per kind (§2.8)
  public string Back;          // the settled meaning, shown when Sealed/Flipped: "상처보다 굵은 줄 — 흉기가 아니다."
  public string Corrected;     // Witness plates: the witness's own correction after 캐묻기 ("등만 봤어… 불이 꺼져 있었고.")
  public string Points;        // what the face reading points at: actor id | "accident" | "suicide" | "natural" | "time" | "place" | null
  public List<string> Alibi = new List<string>();     // actors the face places elsewhere (alibi plates; §2.2 pairing rule)
  public Axis Axis; public Channel Channel;           // foundation enums (MurderModel.cs)
  public List<string> Breaks = new List<string>();    // DeckClaim ids this plate contradicts (true plates)
  public List<string> Supports = new List<string>();  // DeckClaim ids its face reading props up (fakes) or confirms (true)
  public List<string> Routes = new List<string>();    // fakes: flip routes "pl:<id>" | "pl:<a>+pl:<b>" | "ask:<actor>" | "fact:<key>" | "rule:<id>" | "stage:<axis>"
  public List<string> Needs = new List<string>();     // true plates whose force needs a partner or a plaque ("pl:<id>" | "plaque:<claim>")
  public List<string> Confirms = new List<string>();  // true: plates with a disjoint root that confirm the same point
  public List<string> Users = new List<string>();     // who will lean on it in the 심판: "theory:<actor>" | "lie:<packLieId>" | "story" (fakes: ≥1 required)
  public string Owner;         // who defends it in the 심판 (stager, witness, secret holder, the person it points at)
  public string Witness, Seen; // Witness plates: who saw/heard, whom
  public int Room = -1; public double T0 = -1, T1 = -1;   // when/where the thing happened (plain time on the card)
  public double FoundAt = -1; public int FoundRoom = -1; public string FoundBy;   // first finder (P01 or an NPC)
  public int Visibility; public bool HouseSealed;         // lock/breaker/bell/roll-call/loan/House books: never fake (SF11)
  public List<Prop> Props = new List<Prop>();             // Logic.Check reads these
  public PhotoSpec Photo;
  public PlateState State; public List<string> History = new List<string>(); // "Flipped·2막·P01", "Borrowed·P09"
}
[Serializable] public sealed class DeckClaim {
  public string Id; public int Layer;          // 1 first impression, 2 mid reversal, 3 truth-side facts
  public Axis Axis; public string Text, Truth, Holder, Trick;   // plain Korean; Holder = who voices it by default
  public Prop Presented, Actual;
}
[Serializable] public sealed class DeckMystery { public string Id, Kind, Text; public List<string> Plates = new List<string>(); public string Claim; }
[Serializable] public sealed class PhotoSpec {   // everything the Game needs to shoot or re-stage the plate; a few hundred bytes
  public string Subject;       // "item:it40" | "trace:tr12" | "body:P05" | "furn:17" | "door:3" | "room:12"
  public int Room = -1; public P3 Pos; public float Yaw, Size;  // subject centre, facing, bounds radius (m)
  public P3 Seam; public float SeamSize;       // the feature the FACE names (true: the telling seam; fake: the tempting feature) — detail crop, loupe, glint
  public int Anchor = -1;      // neighbouring furniture for context ("the tea-table leg")
  public List<string> State = new List<string>();   // "broken","bloody","wet","burnt","ash","dust-outline","open","locked","stopped:22:10"
  public List<string> Parts = new List<string>();   // fragment item ids (shards) framed with the subject
  public double Clock; public float Dark;           // as-found time and light (0 lit … 1 dark)
  public string Hash;          // content hash (root, room, pos@5cm, state tags) → photo store key and re-stage check
}
```

### 2.2 Composition (target 10; fakes 4–6, target chosen per case; hard range 8–12)

At freeze, `FakeTarget` is drawn deterministically (`MurderHash.U01(S, "deck-fakes:" + incident)`: 4 at 25%, 5 at 50%, 6 at 25%); true slots then fill to `10 − FakeTarget`. A slot that cannot be filled honestly shrinks the deck (never invent); a deck may end 9 or 8, and a deck with fewer than 4 real fakes keeps what exists (≥3, logged).

**True slots**, in this order:

| Slot | What | Rule |
|---|---|---|
| Body | the victim as found | always; Yusti's official record gives it to everyone; the face states only what is visible and the plain found time |
| Hinge | the aha's hinge seam | required; visibility ≤2 or re-findable by 재현 |
| Confirm | an independent path for the aha | root disjoint from the hinge; together they include ≥1 B/S/O/R/X; at least one true plate in the deck has a `Needs` partner (usually Hinge + Confirm), which keeps the plain refusal from being a fake detector (§6.5) |
| Seam | one per supporting trick | breaks that trick's L1/L2 claim or flips the staging fake that props it |
| Link / Clear | culprit link (possession, record, prints, one sighting) or the fact that clears the scapegoat | whichever is needed so every fake has a route |

**Fake slots**, one per category first (in this order), then extra Coincidences/Mistakens, until `FakeTarget`:

| Role | Origin chip | Source (recorded things only) | Its natural misreading | Typical flip route |
|---|---|---|---|---|
| FalseAlibi (the boss) | 위장 | the culprit's alibi as recorded: a true sighting or object that covers only the trigger time, the found room, or a double — **worded about a place and a group** whenever others share it ("라운지 탁자에 누룽지 그릇 셋 — 준서·수아·라온") | "그 사람은 그때 다른 데 있었다" | the Hinge (+Confirm) or a revealed fact; ≥2 routes, one containing the Hinge, so it breaks last |
| Frame | 위장 | TrialPack `Story.Planted`, `LieTarget` token, forged note, the scapegoat's item near the scene | "○○가 했다" | Clear, or Clear + Hinge |
| Staged | 위장 / 피해자의 계획 | the L1 presented artefact (the rope end, the stopped watch, the farewell note, the recorded shout) | "사고/자살이다", "그 시각이다" | the Seam |
| Cover | 감싸기 | a protector's after-the-fact act or lie | a second story on top | a true plate, or the protector's own admission when cornered |
| Mistaken | 착각 | a sighting under a disguise, a dark hall, a wrong clock, low confidence | "누가 ○○를 봤다" | `ask:<witness>` (they correct themselves; `Corrected` is filled) or the true person stepping forward |
| Coincidence | 우연 | a non-culprit's recorded act touching the room/window/weapon class | opportunity or means | `ask:<owner>` or a true plate |
| Secret | 비밀 | another resident's unrelated secret lying near the case — only when a physical item exists | motive | `ask:<owner>`; from Ch.2 flipping it offers 덮어 두기/밝히기 |

**Admission rules for fakes** (all hard):
- **Used.** A fake needs ≥1 `Users` entry: the culprit's `Story`/`Lies` cite it, a `TheorySeed` holder's basis is it, or a resident's own fragment/sighting/relation makes its misreading natural for them (the finder, the witness, someone with a grudge toward `Points`). A fake nobody would lean on is left out and the deck shrinks. The debate director guarantees every admitted fake is cited by ≥1 slide or counter during the 심판.
- **Spread.** ≤2 fakes may point at the same actor (`Points`); the rest point at a reading (accident, a time, a place) or at other people.
- **Alibi pairing.** Whenever a plate places the culprit elsewhere, the deck also places ≥1 innocent elsewhere — on the same plate (a group) or on a true Clear plate. A plate never carries an alibi for the culprit alone unless an innocent's alibi plate exists.
- **Secret never alone.** A Secret fake is admitted only if at least one other plate bears on the claim its misreading supports (the director re-checks this per floor, §6.5).

Rules for the whole deck: ≤2 Witness plates (true and fake together); ≤1 Memory plate, only when a foreshadow the player saw is a path root; no two plates share a root; House-sealed records are always true; every fake has a route present in the deck (or an `ask:` holder alive who knows it, a `fact:`, a `rule:` or a `stage:` axis); a second murder in the same chapter contributes ≤2 plates to the judged deck (the link between the cases).

**Ranking** (deterministic, no RNG): slot priority, then channel (B/S/O/R/X before W/P), visibility (lower first), coverage (covers a claim no other plate covers), reachability on the player's route (the found room and adjacent rooms first), users (more residents who would cite it first, for fakes), then ordinal root compare. Display number: body = 1, the rest ordered by `MurderHash.U01(S, "deck:" + incident + ":" + root)`.

### 2.3 Sources (one pipeline, three feeders)

`DeckSources` asks each available feeder for `PlateCandidate`s and claims, merges by root (first feeder wins), then fills slots. Feeder order: **Case** (foundation `CaseFile`: `KeyClues` incl. `Fake`/`Origin`, `Paths`, `Layers`, `Runs[].Seams`, `Questions[].Theories` for `Users`) → **Pack** (`CaseApi.TrialPack`: `Story.Planted` → Frame/Staged, `Story.ClaimText/ClaimRoom/ClaimWith` → FalseAlibi, `Lies[].BrokenBy` → true breakers and `Users` "lie:<id>", `Roles` protector → Cover, `Foreshadow` with observers → Witness/Memory candidates, `Truth.Preparations` → Link) → **Legacy** (today's data). **`TrialPack` is in code now, so STAGE DECK implements Pack and Legacy; Case is added when `Mur.Cases` is populated** (one registration line).

**Legacy adapter** (`DeckSourceLegacy.cs`; reads state only; never calls the `Impression*` functions because they mutate `TrialState` — factor a pure `Presented(S, inc)` out of the same sources if needed):

| Slot | Today's source |
|---|---|
| Claims (L1) | trace `Note` trick bookkeeping, `SetPieces` steps (Tod, Seal, Message, Swap, KeySlide, Recorder, Accident, Natural, Suicide, Delayed), `MurderPlan.AlibiClaimRoom/LieTarget/Disguise`, the body's apparent cause and time |
| Body | the folded body card (`Evidences.FoldBody` root) |
| Hinge | the seam trace/item of `TrialSystem.ExecutedTrick(S, inc)` (internal, same assembly), else the strongest `CaseBoard.IsKey` card that contradicts an L1 claim |
| Confirm | a second IsKey root with a different channel contradicting the same claim; else a record (DeviceLog, ClockOffset, loans, lock state) |
| Seam | IsKey traces contradicting another L1 claim (staged accident, fake message, moved body) |
| Link | the weapon's owner record/loan, a blood or print trace with `Source == culprit`, else one direct sighting of the culprit in the window (Witness plate) |
| Clear | a sighting or record placing `LieTarget`/weapon owner elsewhere in the window; also the innocent's alibi required by the pairing rule |
| FalseAlibi | the alibi room's object state shared with others, or a true sighting of the culprit at `AlibiRoom`/`AlibiClaimRoom` with its companions |
| Frame / Staged | traces with trick `Note` staging (fake dying message, planted weapon), `LieTarget`'s item near the scene |
| Mistaken | a sighting of a disguised actor, a low-confidence dark sighting, `Statement.Lie` by a non-culprit |
| Coincidence | ledger events by non-culprits in the window touching the found room, its corridor, or the weapon class — only with a user (§2.2) |
| Secret | skipped unless a physical item exists (Cast secrets are backstory text today) |
| Users (legacy) | the culprit for Frame/Staged/FalseAlibi; the witness for Mistaken; the finder or a resident with a grudge/fear toward `Points` for Coincidence/Secret |

### 2.4 Validation (`Deck.Validate`, run at freeze, in `deckdump` and in the roundtrip test)

- 8–12 plates; 4–6 fakes (≥3 only when logged as thin); body present; hinge present; ≤2 Witness; ≤1 Memory; unique roots.
- The aha claim is broken by ≥2 true plates with disjoint roots, ≥1 of them B/S/O/R/X; ≥1 true plate has a non-empty `Needs`.
- Every fake has ≥1 resolvable route and ≥1 user; the boss fake has ≥2 routes, one containing the Hinge.
- ≤2 fakes point at one actor; alibi pairing holds.
- Every true plate breaks a claim, flips a fake, confirms another true plate, or is the body.
- Every root existed at `Frozen` (`Evidence`/`Trace`/`Item`/ledger seq ≤ `FrozenSeq`), except Witness plates whose sighting predates it.
- House-sealed plates are true.
- **Grammar lint** (§2.8): 0 hits of the banned words in `Face`/`Title`; faces follow their kind's pattern; plain times only (0 hits of "새벽 12시"); the jargon list. **Parity report:** mean face length (characters) and pattern-conformance rate for true vs fake plates, within each kind and overall; |difference| ≤10% on length, 0 on conformance.
Failures are logged in `CaseDeck.Log` and in `deckdump`; the build shrinks rather than invents.

### 2.5 Finding plates (no new hooks in the evidence code)

- **Player:** a plate is found when `K(P01).Examined` contains its root (body/item/trace/furn/door) at the plate's visibility, when the player heard the witness state the plate's sighting (`Statements` from `Witness` whose prop `CaseBoard.FactKey` matches), or, for Memory plates, when P01 is among the beat's observers. `FoundAt` is the matching Evidence's `Acquired`. Examinations **before** the freeze count retroactively.
- **NPCs:** the same test on their own knowledge. When the House calls the 심판, every plate only an NPC found is **handed over** as 빌린 은판 (finder's cameo corner), immediately usable; the 서막 shows the hand-over as one gesture ("빌린 은판 2장 — 은결, 태겸").
- **Never a dead end:** a plate nobody found but whose thing is in the found room enters as 늦게 본 은판 from the House's discovery photograph ("발견 때의 사진"); free on 쉬움/보통, one room-strip step on 어려움. The hinge is always also reachable through 재현.
- **Completion without anxiety:** at the first confirm of a chapter, 민혁's inner line once: "다 못 찾아도 심판에서 풀 수 있다." The 서막 shows where each missing plate came from. The investigation HUD shows only "은판 n/10", never the fake count.
- **Testimony is a card only when it is one of the ten.** Witness sheets stay in the kernel (NPC logic needs them) but leave the evidence UI; statements show in 동선 and 인물 as conversation. `claim:cN` court statements are never plates.
- Examining anything else gives the normal caption; it is filed as knowledge (kernel) and listed only in the notebook's folded 메모 ("심판에서는 쓸 수 없는 것").

### 2.6 Engraved mysteries at confirm

`Deck.Mysteries` holds 2–3 riddles phrased as impossibilities, from the L1 claims (foundation `OpenQuestion` when present), never leaking the answer. In the 심판 only one is lit at a time (§6.2); further riddles come from reframes.

| Axis of the L1 claim | Template |
|---|---|
| Access | 「잠긴 {방}」 — 누가, 어떻게 드나들었나 |
| Time | 「{때}의 {일}」 — 그 시각, 정말 그 일이 일어났나 |
| Cause | 「{피해자}의 {겉보기 원인}」 — 정말 {사고/병}이었나 |
| Place | 「{발견 장소}의 {피해자}」 — 왜 하필 거기였나 |
| Identity | 「{때}의 {사람}」 — 본 사람은 정말 그 사람이었나 |
| Means | 「{물건}」 — 무엇으로, 어디에서 왔나 |
| Sequence | 「{소리/불빛}」 — 무엇이 먼저였나 |

Lint (rule 1.12): no riddle set may contain a table-position Place riddle together with a blackout Sequence/Time riddle whose theory seeds include the three families "hid in fear / dropped something / moved after death".

### 2.7 Kernel queries (`DeckQuery.cs`; read-only, deterministic)

```csharp
public static class Deck {
  CaseDeck Of(GameState S, string incident);  CaseDeck Current(GameState S);         // judged incident's deck
  void Freeze(Simulation sim, Incident inc);                                          // called from Cases.HouseConfirm (after CaseApi hooks when they exist)
  Plate Match(GameState S, string rootKey);                                           // photo service and toasts
  bool IsFound(GameState S, Plate p, string actor); string FinderOf(GameState S, Plate p);
  PlateView View(GameState S, Plate p, string viewer);   // public-only view model: title, face, back-if-resolved, corrected, whereWhen, stamp, kind, finder, isTestimony, witness/seen, photoKey
  (int found, int total) Count(GameState S, string viewer);                           // "은판 6/10" (investigation)
  (int total, int fakes, int flipped) CourtCount(GameState S);                        // "은판 10 · 보이는 대로가 아닌 것 5 · 뒤집음 2" (from the 서막)
  PairResult Pair(GameState S, Plate a, Plate b);        // Fits | Clash (never says which) | None — 추리판 (Ch.2+)
  List<(Plate, Plate)> SuggestPairs(GameState S, int max); // 쉬움 proposals: pairs with a Fits or Clash result the player has not tried
  NextHint Next(GameState S, int tier);                  // tier 1 observation · 2 where/whom ("채령 씨에게 아침 일을 물어본다") · 3 what it means
  string WhereWhen(GameState S, Plate p);                // "찻방 창가 · 밤 10시 반쯤 · 민혁이 찾음"
  string PhotoKey(GameState S, Plate p);                 // "<CampaignId>/<loop>-<incident>-<FrozenSeq>-<hash8(root)>"
}
```

### 2.8 Face grammar (one pattern per kind; applies identically to true and fake plates)

| Kind | Title | Face pattern | Examples |
|---|---|---|---|
| Object / Trace / Fixture / Door | the object, optionally with an observable state word (깨진, 젖은, 탄, 찢긴, 열린, 잠긴, 멈춘) or the owner's name when it is a fact | "{물건}. {보이는 상태}. ({정확한 자리})" | "밧줄 끝. 겉이 해져 있다." · "걸쇠에 촛농. 벽 촛대에 짧은 초." · "재하의 소품 상자 안. 둥글게 감긴 피아노 줄." |
| Body | the victim's name | "{자리}, {이름}. {보이는 흔적}. {발견 때}." | "음악실 피아노 옆, 시온. 목에 가는 줄 자국. 손끝과 옷깃 안쪽이 차다. 밤 11시 5분쯤 발견." |
| Record | the record's name | "{기록}. '{그 한 줄 그대로}'" | "대여 장부. '오후 4시쯤, 소품 상자 열쇠 — 라온. 케이블 찾으러.'" · "쪽지. '10시, 열람대에서.' 서명은 '이현'." |
| Witness | "{이름}가 본 것 / 들은 것" | "{때}, {곳}. {본 것·들은 것, 그 사람 말 그대로} — {이름}" | "밤 10시쯤, 불 꺼진 복도. 코트 입은 뒷모습 — 수아" |
| Memory | "기억 — {장면}" | "기억 — {때}, {곳}. {민혁이 본 것}." | "기억 — 아침, 식당. 채령 씨에게 은실을 빌려 가던 해린 씨." |

- **Banned in `Face`:** 보인다, 같은, 듯, 아마, 있을 리, 이상하게, 수상, 분명, 틀림없이. **Banned in `Title`:** 낡은, 수상한, 이상한, 의문의, 가짜. No evaluative adjective anywhere; the face never interprets.
- Length: 18–60 characters per face (Record and Body faces run longest because they quote or list); `Back` 12–40. Parity is measured within each kind (true vs fake Object faces, true vs fake Witness faces …) and overall.
- `Back` states the settled meaning plainly and is shown only after Sealed/Flipped; for a fake it names the origin in the chip, not in the sentence.

---

## 3. Evidence photos (Game — STAGE DECK)

### 3.1 What is photographed

| Plate kind | Picture |
|---|---|
| Object / Trace / Fixture / Door | the thing as found, with 0.6–1.5 m of context (floor pattern, table leg, the tea spreading toward the rug); subject fills 35–50% of the frame width; shards (`PhotoSpec.Parts`) framed together |
| Body | the DiscoveryFilm key still (as found). **Grid, rail, fan and toast show a black-crepe-veiled thumbnail**; the tiered image (gore at the current tier; at 완화 the crepe covers the wound rect) appears only in inspect L |
| Record | the physical object where it sits (ledger, lock logger, breaker panel, clock, the House book) plus a legible paper-strip inset with the one entry in a handwriting font (composited in UI, never rendered text in 3D) |
| Witness | the witness portrait in an oval brass cameo over the room photo of **the place they speak about**, a faint ink silhouette of the person seen (desaturated portrait silhouette), and a clock stamp "밤 10시쯤". After 캐묻기 the card adds the witness's own correction under the face ("등만 봤어… 불이 꺼져 있었고."). (A vantage-view 3D render comes later, when a replay-state API gives observer poses.) |
| Memory | a memory still if one was taken when the player witnessed the beat; else the room photo with both portraits |

**Every plate has a detail image** on `PhotoSpec.Seam` — the feature its face names: for a true plate the telling seam, for a fake the tempting feature (the frayed outside of the rope, the signature on the note). "Has a meaningful 자세히" is never a tell.

### 3.2 When (never during the 심판 — the court pins culling to its floor)

1. **At discovery (as found).** `DiscoveryFilm` offers up to 12 stills of deck-candidate things in the found room (body, traces, items with case tags), one per frame, hidden under its cuts (`PlateCapture.OfferStill(root, camera)`; requested from corpse-discovery; if they are idle, DK2 adds it in a delimited block).
2. **At first examination.** On the player's Evidence event (one delimited line at `Session.cs:326`: `PlateCapture.OnEvidence(e.Actor, e.Data)`), the service captures from the player's own eye ray, reframed (§3.3) — **including in darkness** (the fill retake guarantees a readable frame). If an as-found still exists and the thing has since changed (PhotoSpec hash differs), the as-found still stays the main image and this capture becomes the 자세히 detail; otherwise this capture is the main image and a second tight capture on `PhotoSpec.Seam` becomes the detail.
3. **Before the freeze:** a ring cache keeps the last 24 examination captures as **JPG bytes** (not textures), keyed by root; at freeze, plates claim theirs and the rest are dropped.
4. **Room cache:** a 512×320 room photo on the first entry of each room (one amortised render per room) for Witness plates and fallbacks.
5. **Deck-thing signal:** only for the ten, 민혁 raises a small plate camera (the FP `Photo` motion when the character-polish arms expose it; else a flash-only beat): magnesium puff, shutter, and the plate slides to the notebook icon. Everything else gets the plain caption. This is the main anti-pixel-hunt device and it fires on first examination even in the dark.

### 3.3 Framing and light (`PlateFraming.cs`)

- Candidate 1: the player's eye position moved along the view ray to framing distance; pitch by class (floor things 50–65° down, table 30–40°, wall/door 10–20° off axis); FOV 40°; `CineDof` focus on the subject.
- If blocked: `CineSolver.Place(focus, dir, dist, lift, room, ignore={subject, parts}, minDist)` over 8 directions × 2 distances, scored by 9 bound rays (`Clear`, `PropsClear`, `FurnitureClear`), a context anchor in frame, low clutter, `KeepIn(room)`.
- Culling mask excludes the FP arms, held item, UI, examine outline and transient FX.
- Flash: a spot light parented to the photo camera, enabled only for that camera in `RenderPipelineManager.beginCameraRendering` and disabled in `endCameraRendering`; a warm fill at the key position prevents black frames.
- Luminance check on a 16×16 downsample (worker thread): <0.12 → one retake with double fill; >0.70 → one retake without flash.
- Render by `RenderPipeline.SubmitRenderRequest` (URP single-camera request) or the PaneMontage LiveCam pattern (camera enabled for exactly one frame into a pooled RT). Never `ReadPixels` on the main thread.

### 3.4 Look (`PlateLook.cs` + `Shaders/PlateTone.shader`; display-time, tunable)

- **Album, rail, fan, toast:** hand-tinted daguerreotype — ~70% desaturation outside a soft mask of radius 1.5 × `SeamSize` around the Seam, where the grade is lighter and the subject keeps its key colours (blood red, tea amber, brass gold); soft vignette, hairline scratches, faint halation around flames; brass mat (rectangle for things, oval cameo for people).
- **Inspect L opens in full colour** (the photo as taken, with a thin brass mat); the loupe there is a 2× magnifier, optional.
- **Two-frame M cards by geometry:** when the Seam feature spans <12% of the frame width (wax on a cleat, a scorched fibre, a lock scratch, a signature), the M card shows the context photo plus a detail inset (35% of the photo area) on the Seam. The rule reads only `SeamSize` and framing, never truth.
- State overlays: ◉ 사실 a gold wax seal; ✕ 뒤집힘 the silver blisters out from the Seam and the card turns to its back with a black seal, the origin chip and the breaker's thumbnail. Shapes as well as colours (colour-blind safe). A plate whose picture came from a fallback shows a small "재현" corner mark.

### 3.5 Store and memory (`PlateStore.cs`)

- Disk: `Settings.SaveDir/plates/<CampaignId>/<loop>-<incident>-<FrozenSeq>-<hash8(root)>-{main|detail}.jpg` (JPG q82, 1024×640) + `.json` sidecar (PhotoSpec hash, source: live/discovery/studio/room/icon, clock, luminance, fill). Content-addressed, so autosave rotation, `.previous` copies and loading older saves never desync photos; a hash mismatch triggers a re-stage.
- Memory: thumbnails 256×160 RGB24 for all found plates; full images in an LRU of 2, loaded on inspect and released when it closes. Resident budget ≤8 MB.
- Prune: keep the current and previous 4 loops per campaign (the 현상실 archive), cap 200 MB per campaign.

### 3.6 Fallback chain (0 blank cards; the source is logged for the probe)

1. live capture → 2. solver alternative → 3. as-found discovery still → 4. **specimen studio** (`PlateStudio`: an off-world velvet table at y = −500 with fixed lights; the item's mesh in its recorded state — broken, bloody, wet, burnt) → 5. room photo with a red-ink ring projected at `PhotoSpec.Pos` → 6. engraved icon of the plate kind with "재현". A thing moved after the photo keeps its photo; the card adds one line "지금은 치워졌다 (밤 11시 넘어)".

### 3.7 Cost

Capture ≤6 ms main thread (render request + async readback request), hidden under the R-hold examine motion; JPG encode (`ImageConversion.EncodeArrayToJPG`) and file write on a worker thread; discovery stills one per frame; room cache one render per new room. Probe gate: no frame >33 ms attributable to capture.

### 3.8 Requests (photos)

- **Physics mirror (foundation #4, `Game/Physics`):** shards and debris of deck-candidate items (and of any item with a case trace) are pinned — exempt from the ≤40 debris despawn — until the chapter's 심판 ends; a deterministic `ShatterKit` (4–7 shards cut by planes seeded from the item id, scattered along `Trace.Dir`, with a spill decal) so the broken cup in the photo is the shatter the player walked into.
- **Corpse-discovery:** the `OfferStill` call at the DiscoveryFilm key shots; the wound rect for the 완화 veil.
- **Character-polish:** the FP `Photo` motion; portrait exposure fixes (triage F9/B7) — Witness plates depend on them.

---

## 4. Deck UI everywhere (STAGE DECK)

- **`PlateCard`** — one component in three sizes, used everywhere (notebook, fan, toast, rail, inspect, 현상실):
  - S (toast, rail): photo (veiled for the body), number, title.
  - M (grid, fan): photo (60% of the card; two-frame by the §3.4 geometry rule), number, title (≤12 chars, never truncated), the one sentence, "찾은 곳 · 때".
  - L (inspect): full-colour photo at ≥70% of screen height, wheel zoom 1–2.5×, optional 2× loupe, "자세히" jumps to the detail image, the sentence, the witness correction if any, where/when/who found it, the state history line ("⑥ 뒤집힘 · 1막 · 민혁").
  - Marks: 새것 dot, ★ pin, 빌린 (finder's cameo corner), 늦게 본 (a small House seal), 증언 (cameo), state stamp. Unfound frame: dark with the kind silhouette; the room name on 쉬움 always and on 보통 once the player has entered that room.
- **Notebook 은판 page** (replaces the 단서 tab; NoteUI owns the host, delimited edit): header "은판 6/10" (the fake count is not shown during the investigation; one quiet line under the header, once per case: "몇 장은 보이는 대로가 아니다. 가리는 건 심판에서."); the engraved 수수께끼 with ○/● status; a 2×5 grid (no pages; 1–0 select); a side pane with the 3-line **사건 개요** (알게 된 것 / 아직 모르는 것 / 어긋나는 두 장 — the last line only when the 추리판 is unlocked); the **추리판** (Ch.2+ on 보통/어려움; on 쉬움 from Ch.1 as pair proposals, §5); a folded **기억** list (foreshadow beats the player witnessed); a folded **메모** list ("심판에서는 쓸 수 없는 것"). The notebook opens on this page whenever a case is active.
- **Quick view — key `P`** anywhere outside dialogue: the ten fan across the bottom 30% of the screen over the live world; 1–0 or click opens inspect; Esc/P closes. In the 심판 the rail is already on screen when a floor is open.
- **Toast:** "은판 4/10 — 깨진 찻잔" with the real (veiled for the body) thumbnail and the shutter sound. Non-deck finds keep the plain caption.
- **`PlateRail`** (trial picker): 10 S/M cards in one row, keys 1–0, a **"두 장 함께"** toggle (Shift; always present, never a hint), disabled cards dimmed with a reason tooltip. STAGE DECK bridges it into the current `CluePicker` card column (plates mapped to the player's matching Evidence by root) with a folded "기타 (이번 심판만)" drawer that disappears in STAGE TRIAL.
- **CaseProgress:** "단서 n/m" becomes "은판 n/10" when a deck exists.
- Text ≥18 px at 1080p; UI ≤35% of the frame outside the album and inspect.

---

## 5. Stress-free deduction (what each stage delivers)

| Feature | Behaviour | Stage / owner |
|---|---|---|
| Named question | the engraved 수수께끼 in the notebook and HUD from confirm; in the 심판 the issue line always shows the one active riddle and its goal | DECK: DK1 text, DK3 UI · TRIAL: TR2 |
| 다음 — line | "다음 — 서재 문 아래를 살펴본다" / "다음 — 채령 씨에게 아침 일을 물어본다" from `Deck.Next` (tier 1 observation, 2 where or whom, 3 meaning) | DECK: DK1 + DK3 |
| No pixel hunts | the plate-camera flash on first examination (even in the dark); unfound deck things in the current room glint: 쉬움 always, 보통 after 60 s in the room, 어려움 never | DECK: DK2 + DK3 |
| Completion calm | "다 못 찾아도 심판에서 풀 수 있다" once per chapter; no fake count during the investigation; hand-over and late plates at the 심판 | DECK: DK1 + DK3 |
| 추리판 | pick two plates → "맞물린다 ✔" (gold 추리 도장, a notebook record) / "서로 맞지 않는다" (never says which) / "아직 모르겠다". 쉬움: from Ch.1, proposals ("이 두 장을 맞대 볼까?") from `SuggestPairs`; 보통: from Ch.2; 어려움: from Ch.2, no clash flags | DECK: DK1 `Pair` + DK3 |
| Memories surface | witnessed foreshadow beats appear in 기억; in the 심판 a 3-s recall insert plays automatically when a 가설 touches one ("기억 — 아침, 채령 씨에게 실을 빌려 가던 해린 씨") | DECK: list · TRIAL: TR2/TR3 |
| No dead ends | Borrowed plates handed over, discovery photos for unfound found-room things, 재현 for the hinge | DECK: DK1 · TRIAL: TR1 |
| Recoverable mistakes | written reason + a visible, recoverable cost (§6.11); no game over before the vote | TRIAL |
| Hint tiers by difficulty | 민혁's inner voice, grey italic, never Yusti | TRIAL: TR2 |
| "정해진 것" | ≤5 short lines of settled plaques, always visible during a floor | TRIAL: TR2 |
| Summary before the vote | 3–6 plain sentences in 민혁's voice from the plaques, with a warning for any unflipped fake that the room's version leans on | TRIAL: TR1 + TR2 |

Difficulty is `Profile.Assist`: 2 = 쉬움, 1 = 보통, 0 = 어려움. It changes only the help; deck, fairness and reversals are identical.

---

## 6. The 심판 (STAGE TRIAL)

### 6.1 Shape (contested case, standard act shape; Yusti rings each bell)

| Part | Min | Readable beats | Counted decisions | What happens | Layer |
|---|---|---|---|---|---|
| **서막 · 개정** | 1–2 | 2 | 0 | Non-readable: the lift down, roll call, the hand-over of borrowed and late plates (one gesture + a strip "빌린 은판 2 · 발견 때의 사진 1"). Readable: (1) Yusti's opening with the price ("틀리면, 한 분이 제비로 대신 가십니다. 김민혁 님도 그 제비 안에 계십니다."); (2) the count "은판은 열 장입니다. 그중 다섯 장은 보이는 대로가 아닙니다." — the first time, 민혁's grey inner line fades in beneath it: "사진은 다 진짜다. 다섯 장은 처음 떠오르는 뜻이 틀렸을 뿐." (numbers from the deck). One riddle lights on the clock floor; the others stay dim. | L1 |
| **1막 「첫인상」** | 7–10 | 9–14 | 3–6 | 1–2 riddles about how/what; supporting tricks break; 2–3 fakes flip. **R1**: "it was murder — and it points at ___": the room turns to the L2 suspect in one cascade; bell; headcount. | L1 → L2 |
| **2막 「뒤집힌 이야기」** | 9–13 | 11–16 | 5–7 | The L2 suspect defends (truthfully); the culprit pushes and twists; Frame/Mistaken/Coincidence flip; a side secret may be spared or exposed (Ch.2+). The impossible riddle; the player's **그렇다면…**; **the demonstration** — the whole crime re-enacted on the Memory Stage while everyone, the culprit included, listens (shadow killer); name plates fall to ≤3; the culprit's alibi collapses. **R2**: the room and the gallery eyes turn to the new person; bell; headcount. | L2 → L3 |
| **3막 「반격」** | 6–9 | 7–11 | 4–6 | **지목** (one name, one plate); the accused's candles light; the duel: 2–4 counters by difficulty (§6.11) from their plan (lies within budget, fallbacks, scapegoat, rule shield, attack on 민혁) plus the final demand, answered through ≥3 different actions, filling the knots 기회 · 수단 · 거짓; "증거 대 봐" in their own words; the decisive plate (or the slip); the quiet break (partial admission). Optional **R3** per act shape (§6.13). | L3 |
| **4막 「판결」** | 5–8 | 5–7 | 0 | 「그날 밤의 재구성」 (auto film, ≤30 s, skippable) with 민혁 narrating the summary; the summary card and warning; headcount; the vote (≤5 stance lines); the verdict; **the confession at the stand**; 마지막 한마디; the brief implied execution; the aftermath; the 현상실. | — |
| **Total** | **28–40** | **34–50** | **15–21** | from the first floor to the break a counted decision every ≈2–3 readable beats, never more than 4 | |

**Short hearing** (tier-1 case with a direct witness plus independent proof): 서막 → one riddle → 지목 → 2 counters → film + summary → vote, 12–18 min; ≤20% of 심판s. **Chapter 1** runs the short end of the contested range (28–32 min): 2 counters on 쉬움/보통, no K8/K9, no explicit 추리판, 재현 first shown automatically in the demonstration and offered as an action afterwards.

### 6.2 The unit: one 수수께끼

1. **Engrave.** The riddle lights on the clock floor; the issue line: "수수께끼 2 · 왜 시신은 서재가 아니라 복도에 있었나 — 가설 셋". **One riddle is active at a time.**
2. **Theories.** 2–4 residents voice a 가설 each (one readable beat each, ≤2 subtitle pages). Each makes **one checkable claim** (a second sentence may give colour, never a second target). Each hangs as a painted lantern slide: portrait, the claim, a **basis chip** (봤다 · 들었다 · 짐작 · ○○에게 들었다 · 규칙), and, if it leans on a plate, that plate's thumbnail pinned to it. Supporters show by bodies turning toward the holder and by wax dots on the slide — not by lines.
3. **Floor** (the decision; no timer; music holds a sustained layer). The director **focuses one slide** — "지금 살펴볼 가설" — (the one whose collapse or seal changes the most); the others shrink to ≤2 chips (←/→ switches). The four action buttons are always visible (§6.3); 재현 is greyed with a one-line reason when no axis applies.
4. **Result** (1 beat): collapse (the slide burns; its supporters turn in one staggered cascade), seal, evolve (the holder or another resident amends: "그럼… 옮겨진 거라면?" — a new slide built from what survived), a plain refusal, or a new fragment. The room strip moves on every result that changes a reading.
5. **Settle.** When the true answer stands (backed by proof, rule 1.3, with no standing contradiction) it is engraved as a **brass plaque**, added to the "정해진 것" list, and never restated in dialogue. Proven constraints knock down name plates. The riddle's **reframe** line opens the next riddle ("그렇다면 문제는 '누가'가 아니라 '언제'다"). On key riddles (the aha and the L2 reversal) the settle step is the player's **그렇다면…** choice (2–3 inferences; one follows from the public facts). At least one riddle in three settles through an **evolved wrong theory** (a resident's collapsed theory, amended, becomes the plaque), so the truth does not always arrive from the same kind of holder.
6. **미뤄 두기** appears only after two misses on the same riddle; the riddle then resumes automatically after the next plaque. Otherwise the player never leaves the active riddle, and the director never lets the room idle on it.

### 6.3 Player interactions (4 core actions + rare choices + the finale)

| Kind | Button / key | When | What the player does | Effect | Counted |
|---|---|---|---|---|---|
| K1 수수께끼 고르기 | lantern | after a plaque when ≥2 riddles are available (never in the 서막) | pick the next lantern | order of play; the culprit adapts | no |
| K2 **맞대기** | "사진 내밀기" · 1–0 (Shift: 두 장 함께) | any floor | lay one plate (or two) against the focused slide's claim | §6.5 | yes |
| K3 **캐묻기** | "어떻게 아는지 묻기" · Q | any floor; ≤2 per slide | ask the holder's basis ("직접 보셨어요, 들으셨어요?"); on a rule-based slide Yusti reads the rule verbatim; after a slip "그걸 어떻게 알았죠?" | exposes provenance, forces the culprit to spend a lie, makes an honest witness correct themselves | yes |
| K4 **재현** | "재현해 보기" · R | a slide with a checkable axis (§6.7) | watch the 가설 on the Memory Stage; brass lever: scrub, pause, "만약 ○○가 했다면" | feasible → supports; infeasible → collapses with a plaque ("불가능: 걸어서 12분 — 10시 20분에 닿을 수 없다") | yes |
| K5 더 듣기 / 발언권 | "더 듣기" · Space; E accepts a request | any floor; a resident with information asks for the floor by topic ("은결 — 10시 40분쯤 전시실") | listen, or give the floor to the named resident | the chosen resident brings a fragment or a plate | yes when a request is accepted |
| K6 **그렇다면…** | choice | key riddles | choose the inference | right → plaque + reversal; wrong → written reason, the room strip ticks back, choose again | yes |
| K7 **지목** | name + plate | from the end of 2막; required in 3막 | one standing name and one plate | opens the duel (§6.9) | yes |
| K8 누가 날 봤나 | list | Ch.2+, when anyone attacks 민혁's movements | pick from the residents on 민혁's own 동선 in that window (only people he met) | they confirm; if nobody was on his route, K8 is not offered and the attack is answered by a plate or an honest witness who speaks up | no |
| K9 덮어 두기 / 밝히기 | choice | Ch.2+, when a 비밀 plate flips | spare or expose the unrelated secret | bond with the owner vs the room's trust; carries into daily life | yes |
| K11 투표 | stone | 4막 | drop the stone | the verdict | no |
| K12 마지막 한마디 | 곁에 선다 / 왜냐고 묻는다 / 말없이 본다 | after the confession | choose 민혁's line | bonds, aftermath lines | no |
| K13 미뤄 두기 | button | after two misses on one riddle | set the riddle aside | resumes after the next plaque | no |

Keys when the floor is open: ←/→ slide, 1–0 plate (Enter/click confirms), Shift two plates, Q 캐묻기, R 재현, Space 더 듣기, E accept a floor request. Plate-first selection works too. There is no sentence targeting: a plate is always laid against the focused slide's one claim.

### 6.4 Residents' theories (the content of the debate)

**Inputs per resident** (belief provenance, MS-02): their own `Fragment`s (sound, brush of cloth, glint, smell, glimpse — each with source, time anchor, confidence), sightings/heard sounds/statements in `Knowledge`, public plaques so far, relations (fear, grudge, like, attach toward each person), and personality (`CastTraits`/`Personality`: Fearfulness, Argue, Pride, Empathy, Obs, superstition). The foundation's `OpenQuestion.Theories` (`TheorySeed {Holder, Kind, Text, Basis, True, Lie, Culprit, Conviction}`) are used when present; otherwise the generator builds seeds itself.

**Theory families** × typical holders (the generator picks 2–4 distinct families from distinct holders per riddle; ≥1 points toward the truth or is testable toward it):

| Family | Example (riddle: 왜 시신이 복도에?) | Natural holders |
|---|---|---|
| 스스로 (the victim did it) | "어두운 게 무서워서 나왔다가…" | fearful, empathetic |
| 사고 · 우연 | "뭘 떨어뜨려서 주우러 나왔다가 넘어진 거지" | practical, trusting |
| 옮겨짐 · 꾸밈 | "죽은 다음에 옮겨진 거야. 끌린 자국 봐" | observant, logical |
| ○○가 했다 | "이현 씨 방이 바로 옆이잖아" | grudge/fear toward ○○ |
| 시간 착각 | "비명은 11시가 아니었을지도 몰라" | logical, clock-minded |
| 숨은 길 | "하인 계단이면 3분이야" | architecture-minded (해린, 태겸, 서윤) |
| 엉뚱한 소리 | "저택이 한 짓 아니야?" | dramatic, superstitious — ≤1 per riddle, one beat, laughed or shrugged down, never a decision |

- **Provenance ≥80%:** every non-tangent theory cites a concrete fragment the holder has (a sound and its time anchor, a glimpse, what they touched) — "11시 종 치고 바로 들었어요. 한 번에 뚝 끊겼어요." The family only chooses the shape; the content is the holder's own perception, so the same family sounds different every case.
- **Who holds the truth varies:** the eventually true answer is held by the "sharp" archetype (high Obs + Argue) in ≤50% of riddles; the practical, the fearful and the kind are right often enough that no personality is a tell.
- **Honest twisting:** innocents with a grudge or fear also push and twist (sincerely), and the first resident to accuse someone is the culprit in ≤35% of 심판s.
- **Pile-ons** are shown by the cascade, with at most two spoken lines (the accuser and one echo). NPCs **combine public plates aloud** only for supporting facts, never for the hinge, and only after the player let the floor pass once. Honest residents correct each other ("아니, 코트만 봤다고 했지 얼굴은 아니야").
- **Conviction** comes from the basis (saw > heard > assumed); 캐묻기 exposes it. A theory built on someone else's words names them ("수아가 그랬잖아"), which is how twisted testimony becomes catchable.
- **Every admitted fake is cited** by ≥1 slide or counter during the 심판 (its `Users` from §2.2 are scheduled into the riddle whose claim it supports).
- Lines come from LineBank keys per family × register; cast-voice supplies per-character variants and tells; no line repeats per speaker.

### 6.5 Resolution rules (kernel; `Logic.Check` over typed props plus the deck's route table)

**맞대기 — plate P (or pair P+Q) against the focused slide's claim c:**

| Situation | Result | Visible cost/gain |
|---|---|---|
| P is proof (a true plate with its `Needs` met, a flipped back, a plaque, or a pair forming a route) and contradicts c | **the slide collapses** (burn); if it leaned on a fake F and P ∈ F.Routes, **F flips** (✕ 뒤집힘, origin chip, back sentence); P is Sealed | its supporters turn (cascade); the room strip moves toward 민혁's reading |
| P is proof and supports c | **Seal**; a slide whose claim is sealed and uncontradicted can settle | ◉; the plaque if settled |
| P is a fake laid as proof and someone present can flip it (an `ask:`/`fact:` they know, or another shown plate) | the owner **flips it at once** with a written reason | 쉬움/보통: the flip is the consequence (the plate becomes a 드러난 사실; 민혁's reading loses one supporter on the strip). 어려움: 2 residents lean away and the slide's holder gets one free line |
| P bears on c but its force is not yet established — a fake nobody present can flip, **or** a true plate whose `Needs` partner/plaque is missing | **plain refusal**: "지금은 이 은판만으로는 가릴 수 없다." No state change, no cost; a second refusal on the same slide offers the tier-1 hint | — (the same line answers both cases, so it is not a fake detector; lab gate: ≥30% of refusals hit true plates) |
| P unrelated to c | "맞지 않는다" with a plain reason ("이 은판은 '언제'가 아니라 '어디'를 말한다") | first miss per riddle free; then the slide gains a supporter (the strip ticks) |
| A House-sealed record against a reading of it | "기록은 정확합니다. 읽는 법이 틀릴 수는 있습니다." — the record stands, the reading is tested | — |

- **두 장 함께:** any two plates can be laid together; a pair resolves as its route if it forms one (≤2 two-plate routes per case; this is the big aha set piece: both photographs projected over each other), otherwise as the stronger of the two plates alone.
- **Secret never alone:** the director never focuses a slide where a 비밀 plate is the only plate whose face bears on the claim.
- **Sequence guard:** the director never opens a third consecutive counted floor whose only progressing action is 맞대기; it prefers a slide whose weak point is a basis (캐묻기), an axis (재현) or a resident's request (발언권).
- **캐묻기:** reveals the basis and, for a witness, their fragment detail; an honest mistake corrects itself (a 착각 fake flips by its `ask:` route and its card gains `Corrected`); the culprit must answer with a prepared lie (spending budget) or a fallback; a refusal ("그건 말하고 싶지 않아") is visible and costs the refuser supporters. Yusti answers only rule questions, verbatim.
- **재현:** feasibility in closed form (§6.7); ≤20 s, skippable; offered only when its outcome would change a slide.
- **더 듣기 and first refusal:** NPCs may test and break **supporting** claims only after the player passed on that claim twice, and never the aha; the NPC who does takes the lead of the next riddle.

### 6.6 The culprit fights

**The plan** comes from `CaseApi.TrialPack`: `Story` (ClaimText, ClaimWith, Account, Theory, Scapegoat, Planted), `Lies` (by topic, `BrokenBy`, `Cost`), `Scheme.LieBudget` (2–6), `Fallbacks` (ordered; each `Concedes` a fact and `Keeps` the core), `Shields`, `Roles`. An improvised pack (no scheme) is built by CaseApi from the `MurderPlan`. **The culprit may only say lies from the pack or improvised lies whose seam exists** (fairness: every lie is breakable by something real).

**Before 지목** the culprit is one voice among many: they back the wrong slide that protects them, push the scapegoat, twist honest testimony, spend lies when pressed by 캐묻기, and lean on the FalseAlibi plate. Pressure shows only through face and posture — on everyone equally (§6.9 faces).

**After 지목** the accused's **composure candles** light on their stand: candles = the number of counters this duel will run (쉬움 2 · 보통 3 · 어려움 3–4; Ch.1 ≤2; capped by what the pack still holds, min 2) + 1 for the final demand. Each answered counter snuffs one; the decisive plate snuffs the last. At ≤⅓ lit the CharacterBible tell fires (해린 pulls her goggles down, 도윤 resets the cup, 태겸 checks his pocket watch, 라온 touches the missing earphone, 서윤 says "괜찮아요" twice and aligns her pen). Innocents never get candles; a wrong 지목's accused gets candles that stay lit and go out at once when they are cleared.

**Counter moves** (one selector `Defense.Choose(speaker)` for everyone, driven by stake, knowledge and personality; innocents use the truthful subset). In the duel the counter's **target is an empty knot** (기회 · 수단 · 거짓, §6.9): "기회가 있었다는 증거는?" becomes a where/time lie, "수단은?" an item lie or 딴 가설, "거짓은?" a twist or an appeal.

| Move | Line shape | Player's answer (the verb it favours) |
|---|---|---|
| 딴 가설 (innocent reading) | "새가 운 건 맞아. 시온이 장난친 거겠지." | an independent proof (맞대기) or **재현** |
| 희생양 밀기 | backs the L2 slide, cites the Frame fake | flip the Frame via its route (맞대기) |
| 말 비틀기 (cherry-picked testimony) | "수아가 10시쯤 이현 씨를 봤다잖아." | **캐묻기** the witness: "등만 봤다고 했지." |
| 시간 · 알리바이 | "그때 난 방에서 줄 갈고 있었어." (PackLie where/with/time) | the plate in the lie's `BrokenBy` (맞대기), or **재현** of the route |
| 물러서기 (fallback) | "새를 가져간 건 맞아. 음정 보려고." (Concedes / Keeps) | the step that cannot be innocent — often a resident's honest fragment (**발언권**) |
| 역공 (attack 민혁) | "민혁 씨도 저녁에 서재 들렀잖아?" — only if a defence path exists | K8 (Ch.2+), a plate, or the witness who speaks up |
| 규칙 방패 | see below | **캐묻기** → Yusti reads the rule; then the proof that breaks the shield's premise |
| 감정 · 호소 | a pause, tears, an ally's plea | **더 듣기** (a protector or ally may speak) or 캐묻기 (press; soft-hearted residents cool slightly) |
| 증거 대 봐 (always last) | in the character's own words: "됐고, 증거 대 봐." · "…그래서. 내가 했다는 건 어디 있는데." · "사진 몇 장으로 사람을 잡겠다고?" | the NPC-independent decisive plate, or the slip |

**Duel variety rules:** ≥3 distinct player verbs across the duel (e.g. 맞대기, 발언권, 재현); never more than 2 consecutive 맞대기 decisions anywhere in the 심판; the slip (below) sometimes lands before "증거 대 봐" (MurderHash-deterministic, ≥25% of duels where a slip fact exists), so the final demand is not always the signal of the end.

**Rule shields** (play `RuleShield.When` literally; the `Counter` is always a fact or the rule's exact words). The Ch.1 showcase shields are y6, house-dark and night-lock; y2 first-in is allowed from Ch.2 and never the chapter's only shield.

| Shield | Culprit's argument | How the player breaks it |
|---|---|---|
| y6 second-killer | "두 번째 일은 내가 했어. 하지만 이번 심판은 첫 번째 사건만 가리잖아. 첫 번째와 난 무관해." | prove the link (the same hand in both: a link plate among the ≤2 second-case plates) — or accept it, and the 심판 continues on the first case (지목 reopens, their name stays in the chapter record) |
| y6 order-swap | "내 쪽이 나중에 일어난 거야." | the proof fixing the true order (cooling, a clock, a record) |
| y6 not-deliberate | "사고였어. '일부러'가 아니면 가리지 않잖아." | the preparation (foreshadow/Link) that shows intent |
| CH03/CH23 house-dark | "불은 저택이 껐어. 스위치엔 아무도 손 안 댔어." | the breaker log (House-sealed) showing a manual trip |
| house-night night-lock | "10시 뒤엔 저택이 그 방을 잠갔어." | the lock log + a trace from before 22:00 |
| y7 dead-scapegoat | "죽은 진우가 한 짓이야." | the time the dead person was already dead or elsewhere |
| y2 first-in (Ch.2+) | "소매의 피는 처음 발견했을 때 묻은 거예요." | the blood was already dry/under the fold before the discovery, or the discoverers' order |
| CH08 pair / CH02 wing / CH16 closed-room / CH04 sealed-statement / CH10 inquiry | "짝이랑 계속 같이 있었어" / "다른 날개라 못 갔어" … | the exception window or the partner's honest perception |

**The slip** (optional, never the only path): at ≤⅓ candles, when a detail only the culprit could know is still not public (from `Truth.Preparations`/`Log`), the culprit mentions it once; the next floor offers "그걸 어떻게 알았죠?" (캐묻기).

### 6.7 The Memory Stage (기억의 무대): 재현, the demonstration, the film

- **Where:** the real crime room at 1:1 with the real actors, props and physics — the court's clock floor "opens" (a vertical develop-wipe) into the room, using the floor switch the reveal uses; back to the court afterwards. A plain-time clock runs in the corner ("밤 10시 20분쯤"); time-lapse where needed.
- **Posing:** from the recorded frames (`ReplayStage.StateOf`/`Seek`/`Advance` today; a kernel replay-state API when the foundation adds one): everyone — the victim included — is posed **alive** (standing, walking, animated) until their recorded death tick; at that tick the body collapses through `PhysicalRagdoll` (the `ReplayStage` collapse path, made public by the cinematics owner), never a pre-posed corpse. A **hypothesis** sketch uses the hypothesised death time and the hypothesised actor's own rig driven by a kernel `StageScript`; it is labelled "가설" and plays the failure visibly.
- **재현 axes** (one verb, many hands-on moments; the axis comes from the slide's claim; each outcome is a closed-form kernel check):

| Axis | What the player sees and touches | Kernel check |
|---|---|---|
| Timing | time-lapse: a candle burning to a line, a body cooling in an open-window room, ice melting | published burn/cool/melt constants (house rules) |
| Route | the house map with walking minutes; the lever moves a figure along the route against the clock | nav-graph minutes (MS-03), public movements |
| Sequence | the witnesses' sound and sight fragments laid on a timeline strip; the lever orders them and the room replays in that order | fragment time anchors (MS-02) |
| Sight | move a lamp or candle; the light cone shows who could have seen whom | room light + positions at the tick |
| Reach | the figure's height against the fixture, the arm reaching (or not) | `CastTraits` height + published reach constants |
| Access | the lock, bolt or latch working (or not) from each side | door/lock mechanics, lock log |

- **The demonstration = the reconstruction** (once per case, at the aha, Kindaichi's shape: the crime explained while the culprit listens, then the name, then the rebuttal). It plays the settled story from preparation to discovery: ≤20 s of the failing room hypothesis (the scapegoat's own rig), then ≤25 s of the truth with a **shadow figure** sized by the settled constraints; 민혁 narrates in ≤3 captions; name plates fall as each constraint is shown; the face appears only after the 지목 holds. Skippable after the first viewing.
- **Auto sketches:** at most 3 per 심판 (the L1 impression, the L2 accusation, the demonstration) play without being asked, 5–8 s each, as non-readable beats (the demonstration counts as one).
- **「그날 밤의 재구성」 (4막):** a non-interactive film (≤30 s, always skippable) of the room's settled version, built by `DebateApi.Reenact(S, "settled")` from the plaques and the 지목 — victim alive until the settled death tick, then the ragdoll collapse — while 민혁 narrates the summary sentences. After a wrong verdict the truth plays through the cinematics owner's `CaseRecap`.

### 6.8 The room: lean, cascades, headcounts, name plates, plaques, the vote

- **Lean:** each juror has a public reading (a slide/candidate, Firm 0–3) derived only from what was said and shown; private suspicion seeds only their first stance in the 서막. Shown by posture (0 hands low, looking around · 1 turned toward · 2 arms crossed facing the stand · 3 pointing over the rail; per-character variants, 0.1–0.6 s stagger, never one crowd pose), gaze, and the gallery eyes, which drift toward the largest reading's stand between lines.
- **Room strip** (2 rows, always visible during a floor): "좌중 — 이현 7 · 진우 2 · 모름 7". It moves on **every** miss, flip, collapse and seal, so the one stake is always legible. Trust toward 민혁 only decides *who* follows first and adds a small, visible bias; there are no hidden thresholds.
- **Cascade:** when a slide burns or a fake flips, everyone whose reading rested only on it drops to undecided in **one** staggered beat; others lose one Firm; ≤6 residents change per beat.
- **Headcount** only at R1, R2 and before the vote: Yusti calls each reading; hands rise, staggered; one non-readable beat.
- **Name plates** (명패) stand on every stand; each settled constraint ("밤 10시 20분쯤 음악실에 닿을 수 있던 사람", "기계새를 감을 줄 아는 사람") turns down the plates of those who fail it (public traits and public movements only). ≤3 stand after R2. The culprit's plate never falls (constraints are true).
- **Plaques** (settled facts) are engraved in brass on the clock floor and listed in the **"정해진 것"** HUD (≤5 short lines, newest first, older ones fold into "…외 n"). They are never restated in dialogue.
- **The vote** (kernel, public facts only): each juror votes their final reading on the strip. The reading's score for each standing name x = `3 × knots held on x (by proof) + 1 × unflipped fakes pointing at x that were shown − 3 × plaques clearing x + trust bias toward 민혁's 지목 (≤1.5) + 0.2 × initial suspicion (tie-break)`; family/lover locks never vote their person; nobody votes for themselves (the culprit votes the top candidate other than themselves). `T.Influence` and the ×2 suspicion base are deleted. If 민혁's reading lacks a majority when the vote is called, Yusti asks once "더 하실 말씀이 없으십니까?" and the player gets **one more 맞대기**.

### 6.9 지목, the duel, the break

- **지목** = one standing name + one plate. The plate is placed automatically on the knot it fits (기회 · 수단 · 거짓); if it fits none, it slides off with a written reason ("이 은판은 흉기를 가졌다는 것만 말한다. 썼다는 건 아니다") and the player picks again at no cost. The other knots are empty; **the accused's counters aim at the empty knots**. Each answered counter snuffs one candle; a knot fills whenever proof for it lands (a counter's answer, a flipped fake's back, or the decisive plate). The duel ends when all three knots hold and the decisive plate (or the slip) has landed.
- A wrong name defends truthfully (their plates, their honest witnesses); the room strip drops by 2; 지목 reopens once without further cost.
- The **decisive plate** is an NPC-independent link (possession, a body mark, a record, an experiment).
- **Faces (pressure, not a lie detector):** `SetDarkFace(CorneredStare, 0.3, 1.2 s)` flickers on anyone whose pressure ≥0.4 when a plate lands on them (innocent scapegoats too); `VeiledSmirk` on a proud resident (Pride ≥0.6) whose slide just gained ≥3 supporters, true or false. Pressure = (lies broken + fallbacks forced + plates landed on them + accusations) / (budget + fallbacks + 3).
- **The break — quiet gothic by default** (≤30 s, skippable after the first viewing): the line stops mid-sentence; the court candles dim until only their stand candle gutters; the gallery eyes open wide at once; a slow low-to-face push; `CorneredStare` held at 1; one organ note, 1.2 s of silence, the bell; their own lantern slide burns out and the decisive photograph develops in its place; a partial admission in their voice. `HollowGrin` is allowed at the break only for volatile types (진우, 시온, 해린) and **at most once per loop** (tracked in `NoveltyMemory` as `face:grin`). The full confession waits for the verdict.

### 6.10 4막: film, summary, vote, verdict, confession, execution, aftermath, 현상실

1. **「그날 밤의 재구성」** (§6.7) with 민혁's narration.
2. **Summary card** ("사건 정리"): 3–6 plain sentences in 민혁's voice from the plaques. If a fake still stands that the room's version leans on, Yusti's procedural line plus the card warn: "아직 확인되지 않은 은판이 있습니다. 이대로 투표하시겠습니까?" (쉬움 names the plate; 보통/어려움 give the count). The player may reopen one riddle here (once).
3. **Headcount**, then the **vote:** anonymous stones into a black urn; ≤5 notable jurors say their stance first; the rest nod.
4. **Verdict:** "지목은… 맞았습니다." / "지목은… 틀렸습니다."
5. **Correct:** **the confession at their stand, alive** (60–120 s, skippable after the first viewing): admission in their own voice → three stained-glass memory frames with the real actors (the old wound, the decision, the act — the victim alive until the death tick; `PaneMontage` texture panes) → the victim re-framed (`CaseFile.Reframe`, else the pack's `Logline`/`Log`) → a personal last request → **K12 마지막 한마디** (곁에 선다 / 왜냐고 묻는다 / 말없이 본다). The trial theme drops to the culprit's solo instrument. Then Yusti: "정해진 대로 집행하겠습니다." — a **brief gothic execution with implied horror** (a door, a shadow, a sound; never a machine spectacle).
6. **Wrong:** the culprit's wish is read as the great doors open; every survivor's name seal except the culprit's, **민혁's included**, sinks into Yusti's aquarium; `y_draw`, `y_execution`, `y_verdict_wrong`; if P01 is drawn, a first-person sequence and the choice 결과 지켜보기 / 마지막 저장으로 (`player_out`). The truth (`CaseRecap`) and the culprit's confession still play afterwards, with the culprit's smirk.
7. **Aftermath:** the empty seat; relations change only through **public acts** (who accused whom out loud, secrets spared or exposed, who stood by 민혁); the grudge-from-ballot leak (`Settlements.cs:53`) is removed except under a rule that makes votes public.
8. **현상실:** the plates "develop" one by one with their stamps, who resolved each and how, and where the unfound ones were. The chapter's plates join a per-campaign 사건첩.

### 6.11 Costs, hints and recovery

| | 쉬움 (Assist 2) | 보통 (Assist 1) | 어려움 (Assist 0) |
|---|---|---|---|
| Relevant plates when the floor opens | breathe gold | after the first miss | never |
| Free misses | first per slide | first per riddle | none |
| Inner-voice hint (observation → where → meaning) | after 1 miss or 60 s idle | after 2 misses/passes, or on request | tier 1 only, on request |
| Fake laid as proof (flippable) | the flip; 민혁's reading −1 supporter | the flip; −1 supporter | the flip; 2 lean away + a free line for the holder |
| Plain refusal | free | free | free |
| 늦게 본 은판 | free | free | one strip step |
| Counters in the duel | 2 | 3 (Ch.1: 2) | 3–4 (Ch.1: 2) |
| 추리판 | from Ch.1 as proposals, clash flags shown | from Ch.2, clash flags shown | from Ch.2, no clash flags |
| Closing warning names the unflipped plate | yes | count only | count only |

Recovery: stuck → 더 듣기 (a floor request), a hint, then 미뤄 두기 after two misses; a miss → a written reason and a visible, recoverable consequence (a supporter on the strip, a free opponent line on 어려움); two misses on one slide close only that slide until another fact changes it; the room leaning wrong at the end → one riddle can be reopened before the vote; a wrong vote stands but the truth still plays. No game over before the vote.

### 6.12 Chapters with more than one murder (rule 여섯) and the rule-shield showcase

The 심판 judges `S.Ch.TargetIncident` (the first deliberate killing; `CaseApi.ForTarget`). The deck is that case's, plus ≤2 plates about the other case (the link). Riddles about the second case appear only if a pack shield or a link needs them. Proving that the "first" was not deliberate moves the target: Yusti re-reads 여섯 and names the case now judged (procedure only).

**Showcase (Ch.2 shape, abbreviated):** 22:40 예담 is found at the foot of the service stair (presented: a slip). 23:30 준서 dies in the kitchen (presented: "the stair killer struck again"). Truth: 서윤 pushed 예담 after waxing the third step at 20:00 (민서 saw her with the polish tin — a Memory/Witness plate); 태겸 poisoned 준서, copying the first case. In the 심판 the room first corners 태겸 with the two link plates. 태겸 (y6 second-killer): "준서 일은… 내가 했어. 하지만 이번 심판은 첫 번째 사건만 가리잖아. 예담 씨 일과 난 무관해." [캐묻기 → Yusti reads 여섯 verbatim.] The player can look for a link (there is none: he is telling the truth) and accept; the 심판 continues on 예담. Then 서윤 (y6 not-deliberate): "밀긴 했어요. 말다툼하다가. '일부러'가 아니면 가리지 않잖아요." — if it holds, the first *deliberate* killing becomes 준서's, and 태겸 would be judged instead. The player breaks it with the waxed step (the preparation plate + 민서's sighting): "여덟 시에 이미 계단에 왁스를 칠했어요." The target stays; 서윤's candles light. Rules are the terrain of the argument, and every shield is broken by the rule's own words or a fact.

### 6.13 Variety for a replayed closed circle

- **Riddles** come from the actual case (axis × object/body/sound/light/route), never from a fixed list; theory families rotate by holder personality and are filled with the holder's own fragments; counters and shield tactics come from the culprit's own pack; 재현 axes come from the claims.
- **Act shapes** (chosen at the 심판's start from what the case supports; the least-seen valid shape wins, MurderHash tie-break):

| Shape | What changes | Requires | Cap |
|---|---|---|---|
| 정석 | R1 → R2 → duel | — | — |
| 뒤집힌 첫인상 | the room is right about *who* early and wrong about *how*; the culprit retreats to the y6 not-deliberate shield; the reversal is about intent | a staged accident or a not-deliberate shield | — |
| 감싸기 | a protector's false confession in mid-2막 (R3 moved earlier), broken by what the protector cannot do | a protector role | — |
| 믿었던 사람 | the culprit leads the truth side early (helps flip fakes), then is caught by a slip or the decisive plate | a trusted-ally culprit | ≤1 per loop |
| 이중 허세 | the culprit frames themselves clumsily early (SelfClear), is cleared by a staged Clear, and returns at the end | D3+ case with a SelfClear scheme | ≤1 per run |
| 짧은 심문 | §6.1 short hearing | tier-1 case | ≤20% |

- **Anti-meta targets** (lab, 20 runs per profile; veteran profile = `Profile.Runs` ≥3): the L2 suspect is the culprit in 10–20% of D3+ 심판s (via 이중 허세 and 믿었던 사람); the first accuser is the culprit ≤35%; among people named on alibi plates the culprit is ≤50%; no act shape above 50% for veteran profiles; "증거 대 봐" is never the same wording twice in a loop.
- After each 심판, TR1 records into `NoveltyMemory`: `q:<kind>`, `theory:<family>`, `counter:<move>`, `shield:<tactic>`, `axis:<재현 axis>`, `shape:<act shape>`, `face:grin`. When several valid options exist (which riddle opens, which tangent, which counter first, which shape), the director prefers the least-seen. The lab reports distinct riddle kinds, counter kinds, axes and shapes per 10 runs.
- Across loops the player remembers and residents forget (v2.2 repeat rules): the 사건첩 keeps past photos for the player; residents' theories never cite past loops.

### 6.14 Juice

| Moment | Camera (new partial `TrialDirectorUI.Shots.cs`; the cinematics owner's `TrialDirectorUI.Camera.cs` is only called) | Music (`MusicDirector`) | Eyes (`CourtroomView`) | Sound |
|---|---|---|---|---|
| 서막 | establishing crane showing faces; Yusti over his seat | TrialOpening, intensity 0.2 | closed → open | lift, bell |
| A 가설 | the speaker in Medium, the slide as a soft overlay | TrialDebate 0.35 | `Watch` the speaker | lantern hum |
| Floor open | Behind 민혁 toward the focused slide's holder | sustained layer 0.5, duck 30% | `Watch` 민혁 | wick hiss |
| 맞대기 | Thing insert of the photo (0.4 s magnesium flash) → the holder's Face; **no voiced interjection, no caption burst** | +0.1 | `Stare` 0.5 | brass clack on the lectern |
| Collapse / flip | Top shot, slow turn over the cascade; burn macro on the slide | +0.15 for 6 s | sweep old → new stand | nitrate crackle, chair creaks, low choir |
| Seal / plaque | Hand shot of the wax seal; floor engraving | small rise | `Blink` | wax thunk, chisel |
| Bell / headcount (R1, R2, pre-vote) | wide crane as hands rise | state change per act (Debate → Pressure → Climax) | `Blink` | tolls = act number |
| Memory Stage | develop-wipe into the room; CineShot shots per step | Mystery under the stage | — | clock ticking |
| Counter | Low dutch push on the accused; Over 민혁 | TrialPressure 0.7–0.85 | narrow on the accused | heartbeat under 3막 |
| Break | §6.9 | silence 1.2 s → bell → the culprit's theme | every eye at once | organ note |
| Vote → verdict → confession → execution | urn close-ups; the stand in the stained-glass light | Vote → VerdictCorrect/Wrong → the solo instrument → Execution → Aftermath | follow each stone | stones, aquarium bubbles |

Rules: the framed face is always the speaker's; system lines play over wide shots; no framing of the unframed/black; `MusicDirector.SetIntensity` is driven every beat from the kernel's tension hint; stinger ids are neutral (`plate_lay`, `slide_burn`, `plate_flip`, `wax_seal`, `cascade`, `bell_act`, `stage_open`, `crumble`, `urn_stone`, `verdict_true`, `verdict_false`, `lottery_seal`).

### 6.15 Convenience and the screen

- Auto **off** by default (V toggles; 3 speeds); E/Space advances; hold Ctrl to skip lines already seen (it stops at every floor); **H** opens the backlog with slide results and plate thumbnails inline (click a thumbnail to inspect).
- **On screen during a floor, nothing else:** the issue line (top, one line, changes only at riddle boundaries: "수수께끼 2 · 왜 시신은 복도에 있었나 — 목표: 틀린 가설을 무너뜨리자"); the focused slide + ≤2 chips (top, ≤30%); the rail with the four buttons (bottom, ≤18%); the room strip; the "정해진 것" list (≤5 lines, side). Name plates, plaques, candles and knots live in 3D. UI ≤35% of the frame; no translucent full-screen panels; text ≥18 px at 1080p.
- **Ch.1 teaching cards:** one card per new term at its first use (≤8), dismissed with E, re-readable from the backlog.
- Tab/N opens the notebook on the 은판 page; P the quick fan; 1–0 plates; key hints show only when the key does something.
- Autosave before the 심판 and at each bell (the debate state is serialisable).

### 6.16 Yusti's lines (하십시오체; procedure, rules, verdict only)

"알려 드립니다. 심판을 시작하겠습니다. 정해진 대로, 지목은 한 번입니다." · "은판은 열 장입니다. 그중 다섯 장은 보이는 대로가 아닙니다." (numbers always from the deck's real counts) · "두 번째 종입니다." · "규칙 여섯. 사건이 여럿이면, 심판에서 가릴 범인은 가장 먼저 일부러 목숨을 앗은 한 분입니다." · "기록은 정확합니다. 읽는 법이 틀릴 수는 있습니다." · "지목할 분의 이름을 말씀해 주십시오." · "더 하실 말씀이 없으십니까?" · "아직 확인되지 않은 은판이 있습니다. 이대로 투표하시겠습니까?" · "투표를 받겠습니다. 돌은 한 분에 하나입니다." · "지목은… 맞았습니다." · "정해진 대로 집행하겠습니다." · "지목은… 틀렸습니다."

---

## 7. Worked example — 「열한 시의 외침」 (Ch.2, D2, 보통; MysteryTrickStudy §3.16; culprit 서라온, victim 유시온)

**The case.** 라온 learned that 시온 informed on 라온's band leader — 시온's own older brother — who was jailed. At 16:00 라온 signs out the props-box key on 태겸's loan ledger ("케이블 찾으러") and drops an old coil of piano wire into 재하's props box. About 22:00 수아 sees him carrying the clockwork bird from the 전시실 toward the music room. At 22:10 he invites 시온 to the music room; at 22:20 he garrottes him with the piano's highest string. The bird keeps 시온's shout "야! 놔!" (the loudest phrase since winding); 라온's mutter "…형 몫이야." goes to the second cylinder, which he does not know exists. He winds the bird for the hour and hides it inside the piano. At 22:50 he asks 준서 for late 누룽지 in the lounge "딱 11시에". At 23:00 the hall clock strikes and the bird sings the shout; 준서 and 수아 hear it from the lounge; 라온, "the ear", swears it was a live voice. 진우 had passed the music-room door at 22:55 and hung a candy-wrapper cord on the knob (a habit). Daily life taught the bird's rules (해린's repair scene, the Library's 『태엽 새 다루는 법』).

**The deck** (FakeTarget 5; display order shuffled in play):

| N | Title | Photo | Hidden role | Face | Back | Routes / Needs | Used by |
|---|---|---|---|---|---|---|---|
| 1 | 시온 | discovery still (veiled in grid/rail) | 실상 Body | "음악실 피아노 옆, 시온. 목에 가는 줄 자국. 손끝과 옷깃 안쪽이 차다. 밤 11시 5분쯤 발견." | "밤 11시보다 한참 전에 숨졌다 — 밤 10시에서 11시 사이, 앞쪽." | breaks c1 (time), flips 7, 8 | 은결 (her theory) |
| 2 | 기계새 | the bird between the piano strings, key in its back | 실상 Hinge | "피아노 현 사이에 기계새. 태엽이 감겨 있다." | "종소리에 맞춰 노래하도록 감아 두었다." | Needs pl:3 | — |
| 3 | 『태엽 새 다루는 법』 | the House book open on the reading desk + paper strip | 실상 Confirm (House-sealed) | "도서관 책. '가장 큰 소리는 다음 종소리에 부른다. 작은 소리는 꼬리를 누를 때. 노래 끝에 딸깍.'" | "11시의 소리는 되풀이였다. 꼬리 속에 소리가 하나 더 있다." | Needs pl:2 | — |
| 4 | 대여 장부 | 태겸's ledger on the lounge bureau + strip | 실상 Link (House-sealed) | "대여 장부. '오후 4시쯤, 소품 상자 열쇠 — 라온. 케이블 찾으러.'" | "재하 씨 상자를 열 수 있었던 사람." | flips 7 (who) | — |
| 5 | 수아가 본 것 | 수아 cameo over the dark 전시실 corridor, ink silhouette | 실상 Link (Witness) | "밤 10시쯤, 전시실 복도. 기계새를 안고 음악실 쪽으로 가는 라온 — 수아" | "새를 음악실로 옮긴 사람." | breaks lie L1 | — |
| 6 | 준서·수아가 들은 것 | two cameos over the music-room door | 허상 Staged (위장), Points "time" | "밤 11시 종소리 직후, 음악실에서 시온 목소리로 '야! 놔!' — 준서·수아" | "종이 칠 때 운 것은 기계새. 소리는 밤 10시 20분쯤의 것." | pl:2+pl:3 · pl:1 · stage:sequence | 이현 (opening), 라온 (lie L2) |
| 7 | 재하의 소품 상자 | the open props box, coil on top | 허상 Frame (위장), Points 재하 | "재하의 소품 상자 안. 둥글게 감긴 피아노 줄." | "상처보다 굵은 줄 — 흉기가 아니다. 누군가 넣어 두었다." | pl:1 · pl:4 | 이현, 라온 |
| 8 | 문고리의 끈 | the music-room knob with the twisted wrapper cord | 허상 Coincidence (우연), Points 진우 | "음악실 문고리에 사탕 껍질을 꼬아 만든 끈." | "10시 55분쯤 지나가던 진우가 건 것. 시온은 그보다 먼저 숨졌다." | pl:1 + ask:진우 | 라온 (redirect), 도윤 |
| 9 | 라운지 탁자 | three 누룽지 bowls, spoons, the lounge clock | 허상 FalseAlibi (위장), group | "라운지 탁자에 누룽지 그릇 셋. 밤 11시쯤 — 준서·수아·라온." | "11시에 어디 있었는지는 이 사건을 가리지 못한다." | pl:2+pl:3 · the back of 6 | 라온 (story) |
| 10 | 시온의 쪽지 | the folded note from the victim's pocket | 허상 Secret (비밀), Points 예담 | "시온의 주머니. 접힌 쪽지 — '10시 반, 음악실. 아무한테도 말하지 마. — 예담'" | "사흘 전 쪽지. 예담은 밤마다 시온에게 몰래 노래를 배웠다." | ask:예담 | 가온 (theory) |

Checks: 5 true / 5 fake; Witness plates 2 (5, 6); the aha ("11시의 외침이 곧 죽은 시각") is broken by 2+3 (object + record) and by 1 (body), disjoint and NPC-free; fakes point at 재하 1, 진우 1, 예담 1, a time, a group alibi (≤2 per person ✓); the culprit's alibi (9) also places 준서 and 수아 (pairing ✓); every fake has a route and a user; true plates 2 and 3 each need the other, so laying either alone earns the same plain refusal a fake would; no face hedges.

**The culprit's pack.** Story: "밤 10시부터 10시 반까진 내 방에서 기타 줄 갈았고, 11시엔 라운지에서 준서 누룽지 먹고 있었어." Theory: "재하가 했다" → (after R1) "문 앞의 진우". Lies (budget 3): **L1** where "10시부터 10시 반까진 방에 있었어" (cost 1, BrokenBy witness:수아 → 5) · **L2** ear "생목이었어. 스피커 소리 아니야." (cost 2, expert lie, BrokenBy item:bird + House book → 2+3) · **L3** item "새는 만진 적도 없어" (cost 1; over budget, so he retreats instead). Fallbacks: **F1** "새를 가져간 건 맞아. 음정 좀 보려고. 10시 반쯤 새장에 도로 갖다 놨어." (concedes the bird; keeps "음악실엔 안 갔다") · **F2** (어려움 only) "음악실에 두고 온 건 맞아. 시온이 빌려 달래서. 감은 건 걔야." (keeps "죽이지 않았다"; broken because the bird was wound after the 22:20 recording, when 시온 was already dead: plaque + 1 + 2). Shields: none (one murder, no House darkness; see §6.12 for the showcase). Roles: scapegoat 재하; arranged witness 준서. Foreshadow: 수아's 22:00 sighting (→ 5); the player's own 기억 of 해린 repairing the bird.

**Engraved at confirm:** 「소품 상자의 줄 — 시온의 목을 조른 줄은 어디에서 왔나」 · 「열한 시의 외침 — 그 목소리는 정말 그 시각의 것이었나」. The player found 8; 은결 found 10 (the pocket), 태겸 holds 4 (his ledger).

**Flow** ([n] = readable beats since the last counted decision; **D** = counted decision; *d* = uncounted):

- **서막.** Non-readable: the lift, roll call; 은결 and 태겸 hand their plates over ("빌린 은판 2 — 은결, 태겸"). [1] Yusti's opening and the price. [2] "은판은 열 장입니다. 그중 다섯 장은 보이는 대로가 아닙니다." 「소품 상자의 줄」 lights; 「열한 시의 외침」 stays dim.
- **1막 · riddle 1 「소품 상자의 줄」.** [3] 이현 (봤다): "11시 외침 듣고 달려갔을 때, 옆방엔 재하 씨 혼자였어요. 상자에 피아노 줄까지 있었고요." — claim: *재하 씨 상자의 줄로 졸랐다* (pins 7, cites 6). [4] 은결 (봤다 — she examined the body): "목의 자국은 가늘었어요. 상자 줄은 굵던데." Floor, focus 이현. **D1** 맞대기 1 → the slide burns; 7 ✕ 뒤집힘 ("상처보다 굵은 줄 — 흉기가 아니다. 누군가 넣어 두었다."); five residents turn away from 재하. Plaque: "시온을 조른 줄은 상자의 줄보다 가늘다." [1] 재하: "…고마워. 혼자였던 건 맞아."
- **R1.** [2] 라온 (희생양 밀기): "그 줄이 아니면… 11시에 문 앞에 있던 사람이지. 문고리에 진우 끈 걸려 있던 거 다들 봤잖아." (pins 8). Nine turn to 진우 in one cascade; bell; headcount "좌중 — 진우 9 · 모름 7". Riddle 「문고리의 끈 — 11시에 문 앞에 누가 있었나」 lights. [3] 진우 (truthful): "문 앞에 있었던 건 맞아. 11시 조금 전에. 안은 조용했어." [4] 도윤 (들었다 — his own fragment): "11시 조금 전에 복도에서 발소리 들었어요. 그러고 바로 외침이었죠. 끈을 걸고 들어간 거예요." Floor, focus 도윤. **D2** 캐묻기 진우 (chip →): [1] "10시 55분쯤. 문틈으로 불빛도 없었어. 끈은 그냥… 버릇이야." [2] 가온 (doubt, not agreement): "불 꺼진 방에 시온 씨가 있었다고요?" **D3** 맞대기 1 against 도윤's claim → burns; 8 ✕ (우연). [1] 도윤 amends (evolve): "그럼… 제가 들은 발소리가 진우 씨였고, 그땐 이미 늦었던 거네요." — the evolved theory settles: plaque "시온은 밤 11시보다 먼저 숨졌다." The dim lantern flares: the impossibility.
- **2막 · riddle 3 「열한 시의 외침 — 이미 숨진 시온의 목소리를 모두가 들었다」.** [2] 라온 (the ear, spends L2): "생목이었어. 스피커 소리 아니야. 시신은… 창문 열려 있었잖아. 빨리 식은 거지." (pins 6). [3] 민서 (들었다 — her own fragment): "한 번에 뚝 끊겼어요. 사람 비명은 그렇게 안 끊겨요." [4] 가온 (짐작, pins 10): "예담 씨 쪽지 봐요. '10시 반, 음악실.' 성악 하시잖아요 — 목소리 흉내쯤은." Floor, focus 라온. **D4** 재현 (Timing): the open-window room; time-lapse 23:00 → 23:05; the collar stays warm → plaque "불가능: 5분 만에 손끝까지 식지 않는다"; 라온's slide burns (`CorneredStare` flicker at pressure 0.4). Focus moves to 가온's slide. **D5** 캐묻기 예담 → [1] "그 쪽지는 사흘 전 거예요… 밤마다 시온 씨한테 노래 배웠어요. 아무한테도 말 안 했고요." 10 ✕ (비밀). **D6** 덮어 두기 / 밝히기. 해린 asks for the floor (a chip: "해린 — 전시실의 새장"). **D7** accept (발언권): [1] "음악실에서 소리 내는 게 사람만 있는 건 아니잖아. 전시실 새장, 오늘 비어 있었어." — a recall insert plays (기억: 해린 repairing the bird). [2] 라온 (re-voices L2 as a new slide): "새? 그게 무슨 사람 목소리를 내." **D8** 맞대기 두 장 함께 2 + 3 → both photographs project over each other, the stained glass cracks, the music drops out; 6 ✕ ("종이 칠 때 운 것은 기계새. 소리는 밤 10시 20분쯤의 것."). [1] **D9** 그렇다면…: (a) "11시의 목소리는 기계새가 되풀이한 것 — 시온은 10시 20분쯤 숨졌다" ✓ · (b) "누군가 11시에 새로 흉내를 냈다 — 그 사람은 11시에 음악실에 있었다" · (c) "시온은 11시까지 살아 있었다".
- **The demonstration** (non-readable film, 민혁's 3 captions [1–2]): the clock floor opens into the music room. The failing hypothesis first — 재하's own rig with the thick coil, the wound mark does not match. Then the truth: 22:10 시온, alive, runs a scale at the piano; a shadow figure enters carrying the bird; 22:20 the struggle, "야! 놔!", the cylinder turns; 시온 collapses (ragdoll); the shadow winds the bird and slides it under the lid; time-lapse to 23:00; the hall clock strikes, the lid trembles, "야! 놔!", *click*. Name plates fall: "밤 10시 20분쯤 음악실에 닿을 수 있던 사람" (route atlas + public movements) and "기계새를 감을 줄 아는 사람" → standing: 라온, 해린, 태겸. [3] 라온 (leans on 9): "어쨌든 11시엔 난 라운지에 있었어. 준서, 수아랑. 그릇 셋 봤잖아." **D10** 맞대기 the flipped 6 (its back, a 드러난 사실) → 9 ✕ (위장). [1] 준서: "그러고 보니… 딱 11시에 맞춰 달라고 했어요." **R2**: the gallery eyes snap to 라온; bell; headcount "좌중 — 라온 9 · 해린 2 · 모름 5".
- **3막 · 「새를 감은 손」.** [2] Yusti: "지목할 분의 이름을 말씀해 주십시오." **D11** 지목 라온 + 4 → placed on 거짓 ("재하 씨 상자를 열 수 있었던 사람"). Four candles light on 라온's stand (보통: 3 counters + 1).
  - Counter 1 → 기회 (spends L1). [1] "10시부터 10시 반까진 내 방에서 줄 갈았어. 음악실 근처엔 가지도 않았어." **D12** 맞대기 5 → the lie breaks; 기회 fills; candle out.
  - Counter 2 → 수단 (F1). [1] "…새를 가져간 건 맞아. 음정 좀 보려고. 10시 반쯤 새장에 도로 갖다 놨어." 은결 asks for the floor ("은결 — 10시 40분쯤 전시실"). **D13** accept (발언권): [1] "그 시각 새장은 비어 있었어요. 문도 열린 채로." The fallback breaks; candle out; `CorneredStare` flicker; 수단 still open.
  - Counter 3 → 수단 again, 딴 가설 onto 해린's real skill. [2] "새 감는 거야 해린 씨가 제일 잘하지. 지난주에 그 새 고친 것도 해린 씨잖아." **D14** 재현 (Route, "만약 해린 씨가 했다면"): 해린's public movements (공방 at 22:15 and 22:30, seen by 태겸) against the 12-minute walk → plaque "불가능: 해린 씨는 10시 20분에 음악실에 닿을 수 없다"; her plate falls; candle out; the tell — 라온's hand goes to the missing earphone.
  - Final. [1] "…그래서. 내가 졸랐다는 건 어디 있는데. 새가 내 이름이라도 불렀어?" **D15** 맞대기 3 (its detail: "작은 소리는 꼬리를 누를 때") → the Memory Stage's lever presses the bird's tail; in the silent court the second cylinder plays "…형 몫이야." in 라온's own voice. 수단 fills; the last candle gutters.
  - **The break** (quiet): the line stops; the candles dim to his; one organ note; silence; the bell; his slide burns out and the bird's photograph develops in its place. [1] "…그 새, 작은 소리까지 담는 줄은 몰랐네."
- **4막.** 「그날 밤의 재구성」 (≤30 s: 16:00 the key, 22:00 the corridor, 22:20 시온 alive then the collapse, 23:00 the song) under 민혁's narration. [1] The summary card (5 sentences; no unflipped fake remains). Headcount. *d* the vote 16–1 (라온 votes 해린). [2] "지목은… 맞았습니다." The confession at his stand [3]: "…아 뭐. 맞아. 내가 감았어." — frames: the band leader taken away; the envelope; the winding key — then 시온 re-framed ("시온은 매일 밤 '오늘의 단어'에 형 이름을 적었대."), then the request ("형 곡… USB에 있어. 누가 한 번만 틀어 줘."). *d* 마지막 한마디. "정해진 대로 집행하겠습니다." — the door, the shadow, the sound. Aftermath: 재하 and 진우 at the empty seat; 예담's secret kept or known. 현상실.

Tally: 15 counted decisions (plus the vote and the last word) over ≈36 readable beats (≈30 from the first floor to the break); longest gap 4 (before D1, D1→D2, D3→D4), mean ≈2.0; 7 interaction kinds (맞대기, 캐묻기, 재현, 발언권, 그렇다면…, 덮어 두기/밝히기, 지목); the duel used 맞대기, 발언권, 재현, 맞대기 (never more than 2 consecutive 맞대기 anywhere); four riddles — three debated with 2–3 theories each from distinct holders, the fourth fought as the duel; 8 of 9 theory slides cite the holder's own perception (가온's rests on a plate); the truth was voiced by 은결 (observant), 민서 (practical) and an evolved 도윤, never by the "sharp" 가온; the first accuser (이현) is innocent; every fake was cited. **Ch.1 variant of the same shape:** no K9 (the secret flips as "개인적인 일" and stays private), 2 counters (D12 and D14), no 추리판, 재현 first seen in the demonstration.

---

## 8. STAGE DECK — build plan (starts when clues-and-qol is finished)

**Gate A:** clues-and-qol finished; compile clean; read its final `Evidences`/`CaseProgress`/`NoteUI`/`Hud`/`CluePicker` APIs and adjust names below if theirs changed; read `Sim/Murder/CaseApi.cs` (TrialPack is in code). The old trial keeps working throughout this stage.

### DK1 — kernel deck model, generator, feeders, grammar, testimony rules (Sim)

- **Owns (new):** `Sim/Trial/Deck/DeckModel.cs`, `DeckBuild.cs` (Freeze, FakeTarget draw, slot filler, admission rules, ranking, Validate), `DeckSources.cs` (candidate model + feeder registry), `DeckSourcePack.cs` (reads `CaseApi.TrialPack`), `DeckSourceLegacy.cs`, `DeckQuery.cs` (§2.7 incl. `CourtCount`, `SuggestPairs`, person-naming `Next`), `DeckText.cs` (titles, faces, backs, mysteries via LineBank per §2.8; plain times), `DeckLint.cs` (banned words, patterns, parity, jargon, DR2 riddle-set lint), `Sim/Content/Lines_Deck.cs`, `Tests/BL23/SimTests/DeckDump.cs`. `DeckSourceCase.cs` when `Mur.Cases` is populated.
- **Delimited edits:** `Sim/State/GameState.cs` (`public List<CaseDeck> Decks;`), `Sim/Systems/Cases.cs` (`Deck.Freeze(sim, inc)` at the end of `HouseConfirm`, murders only), `Sim/Systems/CaseProgress.cs` (deck slots when a deck exists), `Sim/Core/Core.cs` (`ClockFmt.Vague`: 23:xx → "밤 12시 조금 전", 00:xx → "밤 12시 조금 넘어"/"자정 무렵"; never "새벽 12시"), `Tests/BL23/SimTests/Program.cs` (dispatch).
- **Provides:** `Deck.*` (§2.7), `PlateView`, `PhotoSpec`, `PairResult`, `NextHint`.
- **Tests:** compile "오류 0개"; `campaign 20260926 3`, `campaign 777 3`, `campaign <random> 3` → faults=0 roundtrip=IDENTICAL; activities fail=0; new `deckdump <seed> [days]` (full deck table with roles, routes, needs, users, validation, lint, the smart investigator's and a passive player's found counts) and `deckgate <n>` (aggregate over seeds 20260926, 777 + n random).
- **Metrics (deckgate, ≥12 seeds, hard):** 8–12 plates in 100% of murder cases, 10 in ≥80%; fakes 4–6 in 100% (≥3 only when logged thin), each of 4/5/6 in ≥15% of decks; the aha broken by ≥2 true plates with disjoint roots incl. ≥1 B/S/O/R/X in 100%; ≥1 true plate with `Needs` in 100%; every fake resolvable and used, boss fake ≥2 routes incl. the hinge, in 100%; ≤2 fakes per actor in 100%; alibi pairing in 100%; ≤2 Witness plates; no root after `FrozenSeq`; unique roots; House-sealed never fake; banned-word hits 0; face-length parity |Δ| ≤10% within each kind and overall; pattern conformance 100%; smart thorough investigator finds ≥8 in ≥80% of cases; passive player holds ≤2; plain-time lint 0 hits of "새벽 12시".

### DK2 — evidence photo capture service (Game)

- **Owns (new):** `Game/Cinema/Plates/PlateCamera.cs`, `PlateFraming.cs`, `PlateCapture.cs` (ring cache, claims at freeze, discovery offers, examination captures incl. in the dark, detail crops for every plate, the deck-thing flash beat), `PlateStore.cs`, `PlateStudio.cs`, `PlateRoomCache.cs`, `PlateLook.cs` (album/rail grade with the Seam mask, full-colour inspect, two-frame rule, body veil), `Shaders/PlateTone.shader`, `Game/Core/AutoProbe.Plates.cs`.
- **Delimited edits:** `Game/Core/Session.cs` (Evidence case → `PlateCapture.OnEvidence`), `Game/Core/AutoProbe.cs` (register the `plates` step), `Game/UI/DiscoveryFilm.cs` only if corpse-discovery is idle (else request the `OfferStill` line).
- **Provides:** `PlatePhotos.Thumb(Plate)`, `PlatePhotos.Full(Plate, Action<Texture2D>)`, `PlatePhotos.Detail(Plate, Action<Texture2D>)`, `PlatePhotos.Source(Plate)`, `PlatePhotos.Release(Plate)`, `PlateLook.Material(PlateState, PlateLookMode)` (Album | Inspect), `PlateLook.TwoFrame(PhotoSpec)`, `PlateRoomCache.Get(room)`, `PlateCapture.OfferStill(string root, Camera cam)`.
- **Tests:** compile; kernel tests untouched; probe `plates` step: examine every deck thing on the route (one in a dark room), log per plate source, luminance, subject fill, capture ms, bytes, detail present; save/load and confirm photos reload by key; delete one file and confirm re-stage.
- **Metrics:** 100% of found plates have a main image and a detail image (0 blank); live or discovery source ≥80%; luminance 0.15–0.65 (dark-room captures included); subject fill 35–50% (±10); two-frame decided by geometry only (probe logs `SeamSize` vs choice); capture main-thread ≤6 ms, no frame >33 ms from capture; resident plate memory ≤8 MB; photos survive save/load and autosave rotation.

### DK3 — deck UI everywhere (Game)

- **Owns (new):** `Game/UI/Plates/PlateCard.cs`, `PlateAlbum.cs` (notebook 은판 page incl. 사건 개요, mysteries, 기억, 메모, the once-per-case quiet line), `DeckFan.cs` (P), `PlateInspect.cs` (full colour, zoom, loupe, detail, witness correction, history), `PlateToast.cs`, `PlateRail.cs` (incl. 두 장 함께), `DeductionBoard.cs` (추리판 with unlock by chapter/difficulty and 쉬움 proposals), `CaseLine.cs` (다음 line + current riddle in the HUD; the "다 못 찾아도" line), `PlateGlints.cs`.
- **Delimited edits:** `Game/UI/NoteUI.cs` (단서 tab → host the album; witness sheets leave the evidence list and show under 인물 as conversation; open on the album when a case is active), `Game/UI/Hud.cs` (evidence toast → `PlateToast` when the evidence matches a plate; "은판 n/10"), `Game/Trial/CluePicker.cs` (card column → `PlateRail` when a deck exists, with the "기타 (이번 심판만)" drawer).
- **Provides:** `PlateCard.Create(parent, CardSize)`, `Bind(PlateView)`; `PlateRail.Show(plates, enabled, onPick, allowPair)`; `PlateInspect.Open(plate)`; `DeckFan.Toggle()`; `PlateAlbum.OpenForProbe()`.
- **Tests:** compile; a grep lint (in `deckdump`) that `Game/` never reads `Plate.True`; UI sanity in the probe.
- **Probe shots (taken by DK2's `plates` step):** d01 notebook 은판 page (found and unfound frames, "은판 n/10" without a fake count, mysteries); d02 inspect L of a broken-object plate in full colour with its detail; d03 a Witness plate (cameo over the place, clock stamp); d04 the P fan over the world with the veiled body thumbnail; d05 the toast; d06 the old-trial rail with plates; d07 a two-frame M card of a small seam; d08 a Record plate with its paper strip; d09 a dark-room capture.

**STAGE DECK integration:** remove shims; compile; kernel tests on three seeds; Unity build + probe `bash C:/Users/리오/BL23Lab/build_probe_retry2.sh deck1 full BL23_trial2 rand`; review the shots; fix obvious issues.

---

## 9. STAGE TRIAL — build plan (after the foundation integrates)

**Gate B:** `TrialPack` is in the kernel (true as of 23:00); the courtroom workflow is quiet; the foundation has populated `Mur.Cases` or 240 min have passed (then riddles, theory seeds and fragments come from the legacy/pack sources). Read the final `CaseApi`/`CaseFile`/`TrialPack`, any replay-state and ragdoll APIs and the courtroom anchors; write the delta.

### TR1 — kernel 심판 engine, culprit/NPC AI, act shapes, metrics (Sim)

- **Owns (new):** `Sim/Trial/Debate/DebateModel.cs` (DebateState, Mystery, Theory (one claim), Plaque, CulpritMind, Knots, Floor (focus + chips), DebateOption, DebateBeat, ActShape), `TrialSource.cs` (reads `CaseApi.TrialPack` + `CaseFile` when present + the deck; `TrialSourceLegacy.cs` otherwise), `Mysteries.cs` (riddles from `OpenQuestion`s or claims; one active; reframes; 미뤄 두기), `Theories.cs` (generator §6.4, provenance, evolve, holder variety, fake scheduling by `Users`), `Culprit.cs` (lie planner, budget, fallbacks, shields, scapegoat, slip, knot targeting; `Defense.Choose` for everyone), `ActShapes.cs` (§6.13), `Debate.cs` (director: bells, riddles, focus, floors, cadence and sequence guards, first refusal, reversals, auto sketches), `Resolve.cs` (§6.5 incl. the plain refusal and pairs), `Room.cs` (lean, room strip, cascade, headcounts, name plates, vote), `Reenact.cs` (`StageScript` per axis with closed-form feasibility; the settled-version script; uses `PhysApi.WhatIf` when present), `Closing.cs` (summary sentences, warning, reopen-once), `Headless.cs` (policies smart / naive / passive / random / oracle; smart never reads `Plate.True` or `Theory.True`), `DebateApi.cs`, `Sim/Content/Lines_Debate.cs`, `Tests/BL23/SimTests/DebateDump.cs`. Owns `Sim/Trial/Deck/*` in this stage (adds `DeckSourceCase.cs` if DK1 could not).
- **Delimited edits:** `Sim/Trial/TrialSystem.cs` (`TrialState.Debate` field; `Begin/Next` delegate to the debate engine when `Debate.Enabled`), `Sim/Trial/Settlements.cs` (ballot-grudge leak; the lottery includes P01; `player_out` read; confession before execution in the settlement order), `Sim/Systems/Testimony.cs` (`CaseWindow` uses the presented L1 window until the hinge breaks), `Tests/BL23/SimTests/Program.cs`.
- **After the gate passes on 3 seeds (delete, fixing every caller incl. SimTests):** `TrialGames.cs` rounds and Arsenal, the topic conveyor and `FinalStep` machine, the RQ quiz and `TrickOptions`, `T.Influence`, `PendingPrompt`, `PickSpeaker` ×2.2, `TrialTricks.cs:50` ×2.0, culprit-only premises and +0.35 will; the `Impression*` code moves into `TrialSourceLegacy` as pure functions.
- **Interface:**
```csharp
public static class DebateApi {
  DebateState State(GameState S);
  DebateBeat Next(Simulation sim);                               // one beat; null when the 심판 is over
  IReadOnlyList<DebateOption> Options(GameState S);              // non-null only while a Floor is open (focus + chips + the four actions + requests)
  DebateResult Choose(Simulation sim, DebateInput input);        // {Option, Theory, Plate, Plate2, Actor, Inference}
  StageScript Reenact(GameState S, string theoryId);             // null → the truth; "settled" → the room's version for the 4막 film
  CaseSummary Summary(GameState S);  VoteResult Tally(GameState S);  RoomStrip Strip(GameState S);
  DebateMetrics RunHeadless(Simulation sim, string policy);
}
public sealed class DebateBeat { public long Seq; public BeatKind Kind; public bool Readable, Counted; public float Secs;
  public string Speaker, Text, LineKey, Mystery, Theory, Plate, Plate2, Knot, Stinger, Face; public float Intensity;
  public List<LeanDelta> Lean; public StageScript Stage; }
// BeatKind: Line, Slide, Floor, Result, Refusal, Cascade, Plaque, Bell, Headcount, Recall, Stage, Counter, Tell, Break, Film, Summary, Vote, Verdict, Confession, Execution, Develop
public sealed class StageScript { public int Room; public double T0, T1, DeathAt; public string Victim, Axis;
  public List<StageMove> Moves; public string Outcome, PlaqueText; public bool Hypothesis, Shadow; }
```
- **Tests:** compile; kernel campaign on 20260926, 777, random → faults=0 IDENTICAL (with a mid-심판 save/load); activities fail=0; `debate <seed> <policy>` transcript with ▶ at counted decisions, ▷ at uncounted ones and beat numbers; `debatefun <n>` gate over 20260926, 777 + ≥8 random × policies; `debatemeta <runs> <profile>` (20 runs per profile: novice, veteran) for the anti-meta and variety numbers.
- **Metrics (hard):** first counted decision by readable beat ≤4; from the first floor to the break, readable beats between counted decisions mean ≤3.0, **max ≤4**; ≥80% of counted decisions have ≥2 options with different next states and the uniform-random policy picks the progressing option with p ≤0.5; max consecutive same-verb counted decisions ≤2; ≥5 distinct interaction kinds per contested 심판 (target 7); 15–21 counted decisions contested; the duel uses ≥3 distinct verbs; ≥3 distinct counter kinds, incl. ≥1 pack lie and ≥1 fallback concession, and the shield whenever the pack has one (100%); innocents make ≥30% of defence moves; 3–5 riddles per contested 심판, each debated riddle with 2–4 theories from distinct holders (the duel's riddle is fought with counters); provenance-backed non-tangent theories ≥80%; the true answer's holder is the "sharp" archetype in ≤50% of riddles; ≥⅓ of riddles settle through an evolved wrong theory; 100% of fakes cited by a slide or counter; ≥30% of plain refusals hit true plates; ≥2 lead-suspect changes (R1, R2) with cascades of ≥4; NPCs break ≤20% of claims the player could break and 0 aha claims; ≤3 spoken agreement lines; 0 repeated lines per speaker; smart correct ≥85%, naive ≤45%, passive ≤20%; smart vs passive tallies differ on ≥90% of seeds; estimated length 28–40 min contested (≤20% short hearings at 12–18); every `StageScript` keeps the victim alive before `DeathAt`; settlement order confession → execution; anti-meta (§6.13) within targets; `HollowGrin` ≤1 per loop; lint: no `IsCulprit` in speaker/lean/vote code, no `K.Suspicion` read in `Game/Trial`, no 재판/학급재판 in player text, plain times, the DR2 riddle-set lint.

### TR2 — trial UI flow and mechanics (Game, non-camera)

- **Owns (new):** `Game/Trial/Debate/DebateDirector.cs` (plays beats, waits, opens floors, sends choices), `DebatePlayer.cs` (event bus: `OnBeat`, `OnFloor`, `OnResult`, `OnStage`), `LanternSlides.cs` (focused slide + ≤2 chips: portrait, one claim, basis chip, pinned plate, supporter dots; burn/develop hooks for TR3), `FloorBar.cs` (the four plain buttons + `PlateRail` + floor-request chips + 미뤄 두기), `IssueLine.cs`, `SettledList.cs` ("정해진 것"), `RoomStrip.cs`, `MysteryPick.cs`, `AccuseSeal.cs` (지목: one name + one plate, knot placement with reasons), `CaseSummaryCard.cs`, `VoteBallot.cs`, `SecretChoice.cs`, `WhoSawMe.cs` (deterministic list from 동선), `LastWord.cs`, `DevelopRoom.cs` (현상실), `TermCards.cs` (Ch.1 teaching cards, ≤8), `TrialKeys.cs` (auto/skip/backlog/keys), `HintVoice.cs`. Owns `Game/UI/Plates/*` changes in this stage.
- **Delimited edits:** `Game/Trial/TrialDirectorUI.cs` (route to `DebateDirector` when the debate is active), `Game/UI/Backlog.cs` (slide results and plate thumbnails inline; term cards re-readable).
- **After the gate (delete):** `CandleInquiry.cs`, `CrossLedger.cs`, `EngravedQuestion.cs`, `ThreadBoard.cs`, `ClockReconstruct.cs`, `TrialMinigame.cs` if unused, the old round panels and the "좌중이 기우는 곳" HUD, the legacy rail drawer.
- **Interface:** consumes `DebateApi`; raises `DebatePlayer` events with the beat and the Unity anchors (speaker view, slide rect, stand transforms).
- **Tests:** compile; headless has no UI dependency; a UI smoke path in TR3's probe.
- **Metrics:** during a floor only the five elements of §6.15 are on screen; UI ≤35% of the frame; text ≥18 px at 1080p; the issue line changes only at riddle boundaries; 0 banners without an interaction; Ctrl skip stops at every floor; Auto off by default; ≤8 term cards in Ch.1.

### TR3 — presentation and juice (Game)

- **Owns (new):** `Game/Trial/Stage/MemoryStage.cs`, `MemoryStage.Script.cs` (plays `StageScript`; alive-until-death; ragdoll collapse; hypothesis rigs; shadow figure), `MemoryStage.Axes.cs` (timing time-lapse, route map, sequence timeline strip, sight cone, reach, access), `MemoryLever.cs`, `ReconstructionFilm.cs` (the 4막 settled-version film; calls `CaseRecap` for the truth after a wrong verdict), `Game/Trial/TrialDirectorUI.Shots.cs`, `Game/Trial/Juice/TrialJuice.cs` (music states/intensity, stingers, eyes, tension), `CrowdLean.cs` (postures by Firm, staggered cascades, headcount hands at R1/R2/pre-vote), `NamePlates.cs`, `FloorPlaques.cs` (engraved riddles and plaques on the clock floor), `ComposureCandles.cs` (accused only, lit at 지목), `KnotRail.cs` (the three knots on the accused's rail), `DarkFaces.cs` (pressure → `SetDarkFace`; the grin budget), `DevelopFx.cs` (burn/develop on slides and plates), `ConfessionFrames.cs` (stained-glass frames via `PaneMontage` texture panes), `VerdictStaging.cs` (urn, stones, the confession at the stand, then the brief execution; aquarium lottery), `Game/Core/AutoProbe.Trial.cs`. Owns `Game/Cinema/Plates/*` changes in this stage.
- **Delimited edits:** `Game/Core/AutoProbe.cs` (register the trial probe; seed defaults at lines 312/325 read the `-seed` argument instead of 20260926).
- **Interface:** subscribes to `DebatePlayer`; props parent to existing stand/court anchors (read-only; no court geometry edits); uses `ReplayStage` (`StateOf`, `Seek`, `Advance`, `HoldProp`, the collapse path) and `ActorRig.BeginRagdoll`/`PhysicalRagdoll`; `CourtroomView.Watch/Stare/Blink/SetTension`; `MusicDirector.SetState/SetIntensity/Stinger`; `ActorRig.SetDarkFace`; `CaseRecap`.
- **Probe shots (`AutoProbe.Trial.cs`, smart autopilot):** t01 서막 with one lit riddle and faces; t02 first floor: focused slide + chips + rail + strip + "정해진 것"; t03 맞대기 burn with a ✕ card; t04 cascade top shot (turned bodies, no identical poses); t05 캐묻기 with an honest correction on a Witness card; t06 a 재현 axis failing with its plaque; t07 demonstration frame **before** the death tick (victim alive, standing) and t08 **after** (collapsed ragdoll); t09 name plates down (≤3 standing); t10 지목 with the plate on its knot and candles lit; t11 counter with `CorneredStare`; t12 rule shield (Yusti reading the rule); t13 the quiet break; t14 the 4막 film frame with the victim alive; t15 summary card; t16 urn vote; t17 confession frame at the stand; t18 현상실. Also log beats, decisions (counted/uncounted), gaps, verbs, kinds, counters, act shape, estimated minutes, and a per-frame victim-alive check during every stage playback and film.
- **Metrics:** 0 exceptions; victim alive in 100% of stage and film frames before the death tick and a ragdoll collapse at it; the framed face is the speaker's in ≥95% of line shots; no black/unframed shots; music intensity changes ≥1 per bell; ≥1 dark face per contested 심판, and innocents under pressure get `CorneredStare` too; candles only on the accused; 60 fps with the Memory Stage open.

**STAGE TRIAL integration:** remove shims; headless verdicts for smart and naive on 4 seeds; compile; kernel tests; `debatefun` and `debatemeta` vs the targets; then the verify rounds (`build_probe_retry2.sh trial2_<r>a trial BL23_trial2 rand` and `… 777`).

---

## 10. All gates in one place

| Area | Gate |
|---|---|
| Build | compile "오류 0개"; campaign 20260926 / 777 / random → faults=0 roundtrip=IDENTICAL; activities fail=0 |
| Deck | 8–12 plates, 10 in ≥80%; fakes 4–6 with each value ≥15%; aha ≥2 disjoint true roots incl. ≥1 non-witness; ≥1 true plate with `Needs`; every fake resolvable and used; ≤2 fakes per actor; alibi pairing 100%; ≤2 Witness plates; no invented roots; smart finds ≥8, passive ≤2 |
| Deck grammar | 0 banned words in faces/titles; pattern conformance 100%; true/fake face-length |Δ| ≤10% within each kind and overall |
| Photos | 0 blank; a detail image for 100%; live/discovery ≥80%; luminance 0.15–0.65 incl. dark rooms; fill 35–50%; ≤6 ms main thread; ≤8 MB; survive save/load; two-frame by geometry only |
| Cadence | first counted decision ≤ beat 4; first floor → break: max 4 readable beats between counted decisions, mean ≤3.0; ≥80% meaningful decisions (random p ≤0.5); max 2 consecutive same verb |
| Interaction | ≥5 distinct kinds per contested 심판 (target 7); ≥3 verbs in the duel |
| Culprit | ≥3 distinct counter kinds; pack lies + fallback + shield used when present; counters aim at empty knots; candles only after 지목 on the accused |
| Debate | 3–5 riddles; one active; 2–4 theories per debated riddle from distinct holders; ≥80% provenance-backed; "sharp" holds the truth ≤50%; ≥⅓ settle via an evolved wrong theory; ≥2 reversals with cascades ≥4; NPCs never break the aha; ≥30% of refusals on true plates |
| Outcome | smart ≥85% correct, naive ≤45%, passive ≤20%; smart ≠ passive tallies on ≥90% of seeds |
| Anti-meta (20 runs/profile) | L2 suspect = culprit 10–20% (D3+, veteran); first accuser = culprit ≤35%; culprit ≤50% of people on alibi plates; no act shape >50% (veteran); `HollowGrin` ≤1 per loop |
| Length | contested 28–40 min (estimate + probe timing); short hearings ≤20% at 12–18 min; 4막 ≤7 readable beats |
| Order | confession at the stand before the execution; no placement quiz after the break |
| Recap physics | victims alive until the (hypothesised) death tick in every stage, film and confession frame; ragdoll collapse; no replay pose in photos |
| UI | ≤8 new terms in Ch.1; during a floor only issue line, focused slide + ≤2 chips, rail, room strip, "정해진 것"; UI ≤35%; text ≥18 px |
| Lint | no 재판/학급재판; plain times; no `IsCulprit` in speaking/lean/vote; no `K.Suspicion` in trial UI; no `Plate.True` in `Game/`; no DR vocabulary; the DR2 riddle-set lint |

---

## 11. Requests to other workers

| Owner | Request |
|---|---|
| murder-foundation / murder-simulation | populate `Mur.Cases` (`CaseFile`) and add `CaseApi.Of`; `KeyClue.Fake/Origin` with flip routes and the residents who would lean on each fake (`TheorySeed.Basis` → clue key, for `Plate.Users`); `OpenQuestion` kinds, order, reframes and `TheorySeed`s; `Fragment`s per witness with time anchors; a replay-state query (`Replay.StateAt(S, actor, tick)`) for hypothesis staging and future vantage photos; `PhysApi.WhatIf` and published constants (burn/cool/melt, reach, walking minutes) for 재현; the order `CaseApi` hooks → `Deck.Freeze` in `HouseConfirm`; **re-dress the chandelier/candle-thread principle (C6-1) away from Ch.1** (a book-lift counterweight in the Archive, a bell rope, or a night-lock trap) |
| physics mirror (foundation #4) | pin case debris until the 심판 ends; `ShatterKit` |
| corpse-discovery | `PlateCapture.OfferStill` at DiscoveryFilm key shots; the wound rect for the 완화 veil |
| cinematics | keep `ReplayStage.StateOf/Seek/Advance` public; make the collapse call public; texture panes in `PaneMontage`; the develop-wipe transition into a room and back; `CaseRecap` callable for the truth after a wrong verdict |
| cosmic-courtroom | read-only anchors: clock-floor engraving ring, each stand's rail (name plate, candles, knots), the urn spot in front of Yusti, the aquarium |
| character-polish | FP `Photo` motion; per-character Firm 0–3 postures and hand-raise variants; `SetDarkFace` on every rig (or a fallback); portrait fixes (F9/B7) |
| cast-voice | per-character lines for each theory family filled from fragments, basis answers, honest corrections, counters, fallbacks, shield arguments, per-character "증거 대 봐" wordings, the quiet break and the confession, with CharacterBible tells for culprits and innocents alike |
| audio | the neutral stingers of §6.14; a heartbeat layer; the culprit solo-instrument drop |

---

## 12. Keep / rewrite / delete

- **Keep:** `Evidence` filing as kernel knowledge; `Logic.Check`; `CaseBoard.FactKey/Plain/Sentence`; `Settlements` (fixed); `TrialState` core (Participants, Seat, Votes); `TrialPortraits`; `Gothic`/`GothicFx`; `Backlog`; `CineShot`/`CineSolver`/`CineDof`; `PaneMontage`; `ReplayStage`; `CaseRecap`; `RevealPlayer`; `CourtroomEyes`/`CourtroomView`; `MusicDirector`; `DiscoveryFilm`.
- **Rewrite:** `TrialSystem.Begin/Next` → the debate engine; `NpcVote` → `Room` vote; `CluePicker` → `PlateRail`; NoteUI 단서 → 은판 page; Hud toast → plate toast; `CaseProgress.Clues` → deck slots.
- **Delete (after the gates):** listed in TR1 and TR2 (incl. `ClockReconstruct`, now without a successor quiz).

---

## 13. Originality and the Danganronpa guard

Structure borrowed, and re-ordered toward the detective-story solution scene: the gathering, the riddle of the impossible situation, theories that collapse, **the whole crime demonstrated while the culprit listens, then the name, then the rebuttal**, the culprit's desperate counters, the decisive small thing, the verdict, the confession, the aftermath. The DR end-of-trial skeleton (duel → break → a placement reconstruction → vote → motive → execution) is not used: there is no placement quiz, the reconstruction precedes the accusation, and the confession precedes a brief, implied execution. Surface ours: photographs against painted lantern slides; lies burn and truth develops; residents' own theories with a visible basis; brass plaques and falling name plates; the Memory Stage with a brass lever and six 재현 axes; composure candles lit only at the accusation; Yusti's rules read verbatim and used as shields; stones in an urn and name seals in an aquarium; a quiet break. Nothing moves, scrolls, is aimed at or is timed; no shattering glass; no voiced interjection; no mascot. The flagship example's hinge is a house artefact with published rules (the clockwork bird's two cylinders), not a famous tableau.

---

## 14. Decisions to record (DecisionLog)

1. A case has 8–12 plates (10 in the large majority); 4–6 are 허상, the number varying by case; the count is announced from the 서막, never during the investigation; which plates lie is never shown.
2. Testimony is a plate only when it is one of the ten; everything else is conversation.
3. Proof = ◉ plates, the backs of ✕ plates (드러난 사실), plaques and 재현 outcomes. A 허상 laid as proof is flipped at once by whoever can, otherwise refused plainly; the refusal also answers partner-dependent true plates. No wobble state.
4. The 심판 is a debate of residents' theories over one riddle at a time; plates, questions and re-enactments are the player's tools; each theory has one claim; the director focuses one slide.
5. The culprit's lies, retreats, scapegoat and rule shields come from `TrialPack`; innocents defend with the same moves; no `IsCulprit` weights; candles only on the accused after 지목.
6. 지목 is one name and one plate; the knots fill during the duel; the counters aim at empty knots; the duel uses ≥3 verbs.
7. The demonstration in 2막 is the reconstruction (shadow killer until the 지목 holds); 4막 is film + summary → vote → verdict → confession at the stand → last word → brief implied execution; no placement quiz.
8. Deck grammar is truth-blind: fixed face patterns, banned hedge words, detail crops and two-frame cards for every plate by geometry; ≤2 fakes per person; the culprit's alibi never appears alone.
9. Photos are taken as found (discovery) or as first seen (examination, even in the dark), never during the 심판, stored content-addressed beside the saves; inspect is full colour; the body is veiled outside inspect.
10. Quick deck view is `P`; the notebook opens on the 은판 page during a case; the 추리판 arrives in Ch.2 (쉬움: proposals from Ch.1).
11. Ch.1 teaches ≤8 terms; the break is quiet by default and `HollowGrin` appears at most once per loop; the three-witness shield is not a Ch.1 showcase.
12. The flagship example is 「열한 시의 외침」; the chandelier/candle-thread tableau is kept out of Ch.1.
