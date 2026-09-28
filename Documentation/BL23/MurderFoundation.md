# BL23 Murder Foundation — Bible

`Documentation/BL23/MurderFoundation.md` · v1 (design pass) · 2026-09-27 · workflow `murder-foundation` (Design → critique → revision → Gate → Implement → Content → Verify).
Built from MAP 1–5 (current murder code, physics today, planning docs v1.1/v2.2, genre taxonomy, constraints), `MurderArchitecture_Current.md`, `GapReport_Mystery.md`, `HANDOFF.md` §3.9 and the code at 17:20. Drama inputs still being written: `CharacterBible.md`, `DailyLifeDesign.md`.

> **오너용 요약**
> 1. 저택에 결정론적 물리 규칙을 넣는다. 무게·높이·열·물·전기·불·공기·독·끈과 장치·흔적·시신 변화를 다루며, 범행·트릭·플레이어 행동이 모두 같은 규칙을 쓴다.
> 2. 살인은 접근 → 제압 → 치명 방식 → 도구 → 처리·위장을 조합해 만든다. 치명 방식 40종, 도구·약물 70종 이상, 처리 16종, 위장 21종, 트릭 32종을 목록으로 둔다. 특수한 방은 모두 '살인 장치'가 된다.
> 3. 모든 사건에 트릭이 최소 1개(평균 2개) 있다. 트릭은 시각·장소·신원·원인·수단·출입·순서 중 하나를 거짓으로 만드는 물리적 준비이고, 그 준비 자체가 흔적(이음매)을 남긴다.
> 4. 사건마다 '아하' 하나, 첫인상 → 중반 반전 → 진실의 3층 이야기, 일상 속 복선, 피해자 자신의 계획, 인물다운 동기를 기록한다. 나중에 새로 만들 재판은 이 기록(제시된 이야기 대 진실)을 그대로 쓴다.
> 5. 플레이어가 보는 핵심 단서는 사건당 4~7장이다. 풍부함은 트릭에 두고 카드는 적게 둔다.
> 6. 게임 화면은 커널의 결과를 따라 래그돌·물·불·장치를 보여 줄 뿐, 결과를 바꾸지 않는다.
> 7. 구현은 5명이 파일을 나눠 뼈대를 세우고, 작성자 8명이 목록을 채우고, 살인 실험실(200~300건)이 수치로 검증한다.

---

## 0. Frame

### 0.1 What the player must feel (the bar)
- **G1 Every death is a puzzle.** "Stab and flee" is a failure state. Every case has ≥1 trick (mean ≈2, max 3). A crime of passion is only a trigger: the culprit then *always* improvises a cover-up trick.
- **G2 One aha per case.** It rests on a concrete physical detail (the **hinge**) that the player could have seen before the trial, and it comes from who the culprit is (skills, job, ability, relationships).
- **G3 Physics you can see and poke.** Things fall, tumble, splash, burn, steam, spark, frost over and cool down. The player can use the same rules (light a fire, drop a knife in the pool, time a candle) and re-enact a claim.
- **G4 Few cards.** Each case files 4–7 key clue cards. Everything else can be examined but is never required.
- **G5 Motion for everything.** Every step (tie a thread, stoke a fire, wedge a door) has an animation. Exploration stays first person.
- **G6 Original and gothic.** Structure borrows from Carr/Rampo/Honkaku, Ace Attorney, Golden Idol, Zero Escape and Danganronpa (the quality bar, never a source). No DR names, rooms, minigames, mascot or recreated case tricks. The trial is "심판", all participants are adults, and Yusti runs announcements and procedure.

### 0.2 Hard rules (breaking one is a bug)
- **H1 Authority.** The kernel (`Sim`) decides. The Game only mirrors it. A Unity physics result nobody caused never enters the kernel. A physics result the player caused enters only as quantised player input with provenance, applied at a tick boundary.
- **H2 Randomness.** All randomness goes through `S.R(Stream.X)`. Append `Physics, Murder, Trick` at the **end** of `Stream`, and never rename or reorder existing members. New code never draws from Life/Combat/Perception. For outcome jitter prefer the counter hash `PhysRoll(ruleId, entityId, S.Seq)`, which needs no stored stream state.
- **H3 State.**
  - Every piece of state lives in `GameState`, in two new containers: `Phys` (physics) and `Mur` (murder model), one line each.
  - Store Lists of records with stable ids. Sort by id with `StringComparer.Ordinal`, and never let behaviour depend on the enumeration order of a `Dictionary` or `HashSet`.
  - Use `InvariantCulture`. Use no NaN; a sentinel is `-1`.
  - Durations are in game minutes, never in ticks.
  - Continuous quantities are evaluated in **closed form** from stored parameters and a start time t0. Nothing is integrated per tick.
  - Computed getters on new state types are `[JsonIgnore]`.
  - Enums are append-only. Collections are either always non-null or null-until-used, and old saves must load.
- **H4 Budgets.**
  - Physics tick: +15 µs mean and ≤0.5 ms at p99 per tick on .NET.
  - Composer: ≤2 ms per tick, amortised.
  - Steady-state allocation in per-tick paths is zero: no LINQ, no closures.
  - Save growth: ≤100 KB per campaign day.
  - Traces are aggregated and capped.
- **H5 Lethality gate.** Physics may change anything *non-lethally*. Death or incapacitation happens only through a registered `MechanismDef` inside an admitted act that holds a reservation, or through a registered trap or device installed by an admitted plan (that path covers any victim). Every other hazard is capped at injury Sev ≤2 and unconsciousness ≤20 min.
- **H6 Fair play.**
  - Every falsification has ≥2 independent exposing paths, predicted at admission and verified after execution.
  - At least one path must not depend on a single NPC's survival, cooperation or affinity, or on a power.
  - Evidence is never created after the fact.
  - Planners use only their own knowledge.
  - Forced tests never silently lift knowledge limits.
- **H7 Registration.** An entry is rejected at boot if it lacks any of: motions, seams, ≥2 exposing-path templates, failure modes, LineBank keys. (v2.2 §8.7: "no animation → not registered".)
- **H8 Text is never logic.** Rule keys never come from Korean strings, `Desc`/`Marks`/`Surface` prose or 「」 parsing. Use typed fields and ids only. All new player text goes through LineBank keys and reads as natural Korean with plain times ("밤 9시 반쯤").
- **H9 Roles.**
  - The player has no lethal verbs and no culprit route.
  - Unwitting helpers and after-the-fact protectors are allowed.
  - A deliberate accomplice planned before the crime and judged as co-culprit stays OFF (the data is kept) until the owner decides.
- **H10 The truth is a person.** The final truth is always a person's act. Accident, suicide and natural death exist only as *presented* stories.

### 0.3 Vocabulary
| Term | Meaning |
|---|---|
| Act | One composed murder: Approach → [Subdue] → Mechanism(Agent) → Disposal/Staging, plus Tricks and Drama |
| Mechanism | A registered lethal rule (e.g. `M.Drown.Pool`) with its result type and whether it is bloodless |
| Agent | The weapon, tool, substance or piece of the environment that delivers a mechanism |
| Trick / TrickRun | A `TrickDef` executed: physical setup steps that make one fact false |
| Falsification | (axis, presented claim, true claim, seams, exposing paths) |
| Seam | A trace or record that the setup, or the physics, necessarily leaves |
| Exposing path | A set of observations with disjoint roots that proves a presented claim false |
| Hinge | The concrete physical detail on which the aha turns |
| Layers | L1 first impression → L2 mid-trial reversal → L3 truth |
| Foreshadow | An innocent-looking preparation moment in daily life, recorded for later recall |
| Fixture | A kernel-only device derived from the layout (chandelier winch, chapel bell, trapdoor, fly bar, gallery rail, hatch) |
| Hazard | A physics outcome that can hurt someone; it is lethal only through H5 |

### 0.4 Planning-doc alignment (correcting the earlier terms)
- v1.1 has **6** execution bundles:
  - 대면 충돌
  - 물체가 관련된 위험
  - 지연된 결과
  - 신분 오인
  - 물건 은닉·변조
  - 정보 압박과 철회
- It has **4** result types: 즉시형, 단기 지연형, 장기 지연형, 조건 충족형.
- 15-5 has **11** trick classes. T01–T07 are worked examples, not classes.
- Every `MechanismDef` declares a bundle and a result type. Every `TrickDef` declares its 15-5 class and the six P1451 fields: first hypothesis, why it is plausible, prior clue, conflicting fact, invariant true fact, reversing claim.
- Conflict priority (MAIN p.3): the current user request, then BL22, then BL21, then v1.1. As a result:
  - Physics becomes causal (v2.2 R21-02), but lethal only through registered rules (H5).
  - Gore is allowed in three tiers that give identical clues (§D5).
  - Body cooling is a coarse, *published house rule* that outputs ranges (§A5), not physiology.
  - Staged accident/suicide/natural deaths are a cause axis the user has sanctioned (H10).
- Trap, device and dose lifecycles follow P0493/P1803:
  - Installed → Armed → Triggered | Disarmed | Failed | Expired.
  - Tracked fields include ActualTargets and MaxTargets = 1.
  - A wrong victim stays linked to the installer.
- Long-delay lethals must resolve through a public check before the trial (P0491).

---

## A. Physical world model (`Sim/Physics`, implementer 1)

### A1 Two layers, BotW-style separation
- **Kernel physics-lite.** Agents act on materials: Impact, Cut, Pull, Heat, Cold, Water, Electric, Toxin and Gas. Materials never act on each other directly. The model is 2.5-D: a `P3` position plus a **support** that yields height. It is evaluated when actions happen and on a 1 Hz timer pass. It is authoritative and deterministic.
- **Game physics.** PhysX, particles and shaders play a plausible path near the camera. They always converge on the kernel's end state and never write back (§A9).

### A2 Static properties — `PhysProfile` (registries in `Sim/Physics/Profiles*.cs`)
Profiles are keyed by the existing item/furniture type strings. Defaults come from `Mat` + `ItemDef`/`FurnitureDef`, so `WorldTypes.cs` does not grow. Each new family file overrides entries.

| Field | Drives | Default source / examples |
|---|---|---|
| Mass kg, SizeClass (Pocket, Hand, Arm, Body, Large) | carry, crush energy, container fit | `ItemDef.Mass/Size`, `FurnitureDef.Mass/W·D·H` |
| Edge {None, Blunt, Sharp, Point, Cord, Wire} | wound signature, swap compatibility | `DamageType` |
| Flammability 0–3, Residue {None, Ash, Char, Melt, Survives} | fire rule | Paper/Cloth 3 Ash, Wood 2 Char, Plastic Melt, Metal/Stone/Glass Survives |
| Conductive | electric rule | Metal, Liquid; anything wet |
| Floats / Sinks, SoakMin | water rule | Wood/Paper/Cloth/Plastic float until soaked; Metal/Stone/Glass/Ceramic sink |
| Absorbent | contact transfer | Cloth, Paper, Wood |
| Brittle 0–3 | shatter threshold | Glass/Ceramic (reuse `PropMaterial` thresholds on the Game side) |
| MeltMin@19°C | thermal timers | IceBlock 60, Icicle 20, Candle wax |
| Container {capacity class, closable, lockable, airtight} | contain, hide, airless death | Chest, Wardrobe, Crates, Barrel, ColdLocker, Incinerator, Cart, Trolley, TeaCart, ClockCase, Dollhouse, Piano, Organ, Altar |
| Tippable {pivot edge, force class} | topple | H/min(W,D) ≥ 2.5 and not fixed: Bookshelf, Wardrobe, Clock, ClockCase, Cabinet, DollShelf, WineRack, Shelves, Mirror, CostumeRack, Statue |
| Device {powered, rig-able, water contact, electric clock} | electric rule | Washer, Terminal, Jukebox, Arcade, FilmProjector, FloorLamp, PumpUnit, Boiler, Press, ColdLocker, Fridge, Switchboard, departure board |
| HeatSource {max °C, fuel min, chimneyed} | thermal/fire/gas | Fireplace, Stove, Boiler, Incinerator, Candelabra, Brazier |
| Water {depth m, surface height} | water rule | PoolWater 1.6, ShallowWater 0.15, Aquarium (head-height tank), Sink, Washer, Barrel |
| Dusty | dust outline | Bookshelf, Shelves, Pedestal, DisplayCase, ClockCase, WineRack, FileCabinet, Mantel |

Actors get their own profile in the `ActorPhys` registry: BodyMassKg (from `HeightCm` and build), Strength 0–1 (from job/stats; e.g. 임민서, 강준서 high), ShoeSize class (from height) and handedness (`Cast.LeftHanded`).

### A3 Dynamic state — `GameState.Phys : PhysWorld`
All lists are sparse (only non-default entities), sorted by id, and serialised. `P3` and `Room.Floor` stay as they are, and height is derived from the support.
```csharp
public sealed partial class PhysWorld {
  public List<RoomEnv>   Rooms;     // non-default rooms: ambient segments (t0, °C), gas levels (CO, CO2, Fume) as segments, smoke, humidity, noise mask
  public List<ItemPhys>  Items;     // Support {Floor, Furniture, Container, Water, Hanging, Held, OnBody, Fixture} + SupportId; Wet(level,t0);
                                    // heat segment; Burn(t0,dur); Melt(t0,dur); Contam[≤4] {kind, t0}; Contents {liquid, agent, dose}; StoppedAt (watches)
  public List<FurnPhys>  Furniture; // Toppled {pivot, dir, t}; Dust {disturbedAt, outlineOf}; Fault {stripped, wet, by}; Fire {lit t0, fuel, damper}
  public List<DoorExt>   Doors;     // Bolt {none, open, shot}, KeyIn {side, keyId}, Wedge {side}, Gap class, Keyhole, Transom, Taped; mirrors Door.Locked
  public List<WaterBody> Water;     // from furniture: rect, floor, depth, Level(t0, rate), temp, BloodHaze(t0, amount), filter→WaterRoom, drain state
  public List<Fixture>   Fixtures;  // chandelier (anchor, height, mass, rope→cleat), chapel bell (rope), trapdoor, fly bar + sandbag, gallery rail segment,
                                    // stair top, machine-room hatch; derived from the layout WITHOUT RNG at loop start
  public List<Link>      Links;     // thread / fishing line / rope / wire / cord: anchors A,B; Through (door gap, keyhole, rail, pulley); state; material
  public List<PhysTimer> Timers;    // ordered by (DueClock, Seq): candle burn-through, ice melt, record end, clock alarm, press, drain,
                                    // door auto-close/relock (moved here from Movement), bell
  public List<BodyPhys>  Bodies;    // dead/unconscious: thermal segments (≤8), death pose, livor pose/fixedAt, vital flags, submerged since, carry mode
  public List<Dose>      Doses;     // agent, victim, vector, t_ingest, onset/peak/lethal clocks, rescue window, source item
  public List<Patch>     Patches;   // floor patches: water, blood, oil, ash, soil, flour, wax, chalk, paint; t0, dryBy, size; ≤64 per loop, merged
  public int NextId;
}
```
Fixtures:
- They make the "murder machines" real without changing `Layout` or the layout RNG. `Phys.BuildLoop` places them from room geometry:
  - chandeliers along the long centre line of the GrandHall, Dining, Theater, Chapel, Lounge, Library and Gallery, using the same rhythm as `MansionView.Hanging` (see G5);
  - the chapel bell rope;
  - the stage trapdoor and fly bar on `Stage`;
  - rail segments along `Void` edges;
  - a hatch in the MachineRoom.
- Chandeliers are lowered by Yusti every evening to light the candles. That routine is where the player learns the winch and cleat (a published rule).

### A4 The rule set (12 families; `Sim/Physics/Rules*.cs`)
Each rule has the signature `Apply(PhysAction) → state change + ledger + Phys event + traces + optional Hazard`. Timers or closed-form queries drive them, never polling.

| # | Rule | Condition → state change | Seams it leaves (traces / records / body findings) |
|---|---|---|---|
| R1 | Support & gravity | Support removed (release, cut, burnt link, push past an edge, topple force ≥ class, drop, throw) → next support below; fall height h from `Layout.FloorY` + support heights; injury/crush from the A5 table. Topple rotates the footprint about the pivot edge, invalidates nav and scatters contents within 1 m | Dent/scuff at impact, shard spray, dust puff, rail dust wiped, torn button, **tumble bruises vs palm-shaped push bruise**, crushed footprint zone |
| R2 | Thermal | Room ambient (published base + heat sources + mixing through open doors) → Newton closed form per entity segment; ice/wax melt timers; drying factor | Condensation (cold item brought into warmth, 30–60 min), frost on collar (≥60 min at ≤4 °C), heat-dried skin on one side, puddle ring after a melt |
| R3 | Fire | Source + flammable + contact/proximity → burn progress p(t) = (t − t0)/dur(mass, class); residue by material; smoke and CO if not chimneyed; spreads ≤1 hop per 2 min to flammable furniture; water or burn-out extinguishes | Ash, char, scorch, soot, smoke smell (2 h), surviving remnants (buttons, metal, bone, teeth); **soot in the airway only if the victim was breathing** |
| R4 | Water | Inside a WaterBody rect on its floor → Wet=1; blood → Washed (residue stays), soil/ash wash off, ink runs; float/sink by density and soak time; the pump cycle carries floaters into the filter; drain/fill; drowning = head-under timer while held or unconscious → Hazard(Drown) | Wet prints and drips until damp, splash area, blood haze in the water, filter catch, **froth only if breathing**, clothed-drowning finding |
| R5 | Electricity | Circuit on + fault (stripped cable or water ingress) + contact (using the device's spot, standing in a conductive patch linked to it, or being in energised water) → Hazard(Shock); the breaker trips (logged to the minute), the room goes dark, electric clocks stop (R12), auto-reset after 12 min | Stripped cable, insulation shavings on tool and floor, scorch, electric mark on the hand, breaker log, stopped electric clocks |
| R6 | Air & gas | Blocked flue, damper shut while lit, brazier, darkroom mix, or fermenting casks → concentration as closed-form segments from (rate, room volume, open doors); thresholds: symptoms → drowsy → unconscious → Hazard(Gas); a candle goes out at the CO2 threshold | Rag in the flue, soot on the damper, bitter smell for the first entrants, **cherry-pink lividity (CO)**, a guttered candle |
| R7 | Toxicology | Dose(agent, vector) → onset/peak/lethal clocks in closed form; signs visible to witnesses at scheduled times; potentiation (alcohol + sedative); rescue window (FirstAid/EpiKit/Infirmary) → Hazard(Toxin) at the lethal clock | Residue in the vessel, powder spill, vomit patch, needle mark, sting swelling, symptoms witnessed at dinner, cause time ≠ death time |
| R8 | Mechanisms, links, timers | A link transmits a pull through a gap; transitions: bolt shoots, key turns, latch drops, cleat releases, trapdoor opens, sandbag drops, bell rings, clock stops/sets, gramophone/recorder plays, press fires, drain opens; timers: candle burn-through, ice melt under a weight, record length, alarm, door auto-close/relock | Thread fibre on the bolt knob, pin hole in the frame, wax drip on the cleat, burnt thread end, puddle ring, rope scrape on the rail, key scrape under the door |
| R9 | Contact (Locard) | Contaminants {blood, water, soil, ash, flour, wax, oil, chalk, paint, powder, fibre colour} transfer between actor, item, surface and patch (≤4 per carrier, aged); prints carry the medium plus shoe size / handedness; lifting from a Dusty surface leaves an outline; drag trail (heel scuffs + smear every 2 m); drip trail from a bleeding or wet body being moved | Footprints by medium and size, handprints, **dust outline that no longer matches**, drag trail, drip trail, pool-cue chalk on a sleeve, paint on fingers |
| R10 | Body & post-mortem | At death: pose, room, core 37 °C. Thermal segments; rigor on an effective-minute clock; livor visible at 1 h and fixed at 4 h (effective), with the pose recorded — a move after fixing gives a mismatch, a move before gives a double pattern. Vital flags per wound/exposure. Carry / drag / cart modes with speed from mass vs strength | Apparent death window, livor mismatch/double pattern, bled vs ooze (post-mortem cut), petechiae, ligature angle (hanging vs strangling), hesitation marks, stomach stage vs `Needs.LastMeal`, defence wounds |
| R11 | Sound | Every physical event → `Perception.Sound(kind, pos, loudness)`. Loudness comes from mass × height × material pair; masking sources (boiler roar, rain, music, bell, machine) per room | Who heard what and when, fed through the existing acoustic graph (no new propagation code) |
| R12 | Time sources | Official clock (Yusti) = `S.Clock`; wall clocks with offsets (existing `ClockOffset`) marked mechanical or electric; electric clocks stop at an outage; personal watches (`Grammars.HasWatch`) stop on impact or water; natural clocks: candle length, tea warmth, ice/puddle, wax, drying, a warm furnace, rigor | Stopped hands, breaker minute, clock offset vs official time, candle stub length, lukewarm tea |

### A5 Published constants (house rules the player can learn in daily life; text via LineBank)
- **Ambient temperature:** default 19 °C.

  | Room | °C |
  |---|---|
  | ColdStorage | 2 |
  | WineCellar | 11 |
  | Courtyard | night 8, day 16 |
  | Pool | 26 (water 28) |
  | BoilerRoom | 34 |
  | Incinerator | 30 (42 while burning) |
  | Kitchen | 24 (28 while cooking) |
  | Greenhouse | 25 |
  | RainCorridor | 14 |
  | MirrorWater | 16 |

  A lit fireplace adds +6 °C to its room over 30 min.
- **Bodies:**
  - Cooling is about 1 °C per hour at 19 °C: ×2.5 in water, ×0.6 wrapped or under covers; a heat source holds or raises the temperature.
  - Rigor: jaw at 2 h, full at 4 h, passes at 12 h, all in *effective* hours (cold ×0.3, heat ×1.8).
  - Livor: visible at 1 h, fixed at 4 h (effective).
- **Examination window:**
  - Width = max(20, 90 − 60 × skill) minutes (Obs-based; StateSense 20).
  - Centre = `PhysApi.ApparentDeathClock`.
  - Always shown in plain words ("밤 9시~10시 반쯤").
- **Wetness:** clothes are damp after 30 min and dry after 90 (Pool/Greenhouse ×1.5, BoilerRoom ×0.5). Floor puddles dry in 40–120 min depending on size.
- **Melting and burning:**
  - Ice block: 60 min at 19 °C (20 near a fire, 6 h in ColdStorage). Icicle: 20 min.
  - Candle: 1 cm per 10 min.
  - Tea: scalding for 10 min, warm until 40.
  - Furnace: warm to the touch for 2 h after a burn.
- **Heights:** a storey is 4.8 m (`Layout.FloorY`). Gallery rail → hall floor is 4.8 m. A stair flight is 2.4–4.8 m. The stage trapdoor drops 2.5 m.
- **Fall injury:**

  | Fall height h | Injury |
  |---|---|
  | < 1 m | none |
  | 1–2.5 m | Sev1–2, limbs |
  | 2.5–4 m | Sev3, knock-out likely |
  | ≥ 4 m | Sev3–4, head/neck |

- **Crush energy** E = mass × h (kg·m):

  | E | Result |
  |---|---|
  | < 30 | bruise |
  | 30–150 | Sev2–3 |
  | > 150 | Sev4-capable |

  A dense small object hitting the head from ≥3 m is Sev3. Chandelier example: 120 kg × 3 m = 360.
- **Electricity:** a dry hand gets a Sev1 jolt. Wet skin or standing in water is Sev4-capable.
- **Gas:** CO in a closed room with a blocked flue gives symptoms at 20 min, knock-out at 45 and becomes lethal-capable at 90. An open door halves the rate.
- **House facts announced or posted in daily life** (these make tricks fair):
  - Yusti runs the incinerator every night at 22:00.
  - The pool filter runs at 06:00.
  - The Pool, Dining, Kitchen and Theater are night-locked by Yusti at 22:00 (`Butler.cs`, `NightPolicy`).
  - Chandeliers are lowered on their winch at dusk.
  - The chapel bell rings at 07:00 and 22:00.
  - The cold-store door swings shut on its spring and opens from outside only.
  - Electric clocks stop in an outage.
  - The aquarium's spined fish carry a warning plate.
  - The mansion's windows do not open, so there is no window exit (this keeps locked rooms honest).

  All of these are game rules, not forensic claims. The Korean text never quotes numbers the player can't use.

### A6 Hazards and the lethality gate
```
rule → Hazard {Kind: Fall|Crush|Cut|Stab|Blunt|Choke|Drown|Shock|Gas|Toxin|Burn|Cold, Victim, Sev, CauseSeq, Source, Installer, Plan?}
Lethality.Resolve(sim, h):
  if MurderApi.Admits(S, h, out plan)  // active reservation and matching act mechanism, OR a device/trap/dose installed by an admitted plan (any victim)
       → full outcome via Strike/ApplyWound/Die(cause = h.CauseSeq) + MurderApi.OnCause(plan, seq, kind)
  else → capped (Sev ≤2, KO ≤20 min) + ledger PhysHazardCapped
```
- A wrong victim of a trap, device or dose keeps `Incident.PlanId`, sets `IntendedVictim ≠ Victim` and `Murder = true`. This fixes both the "Direct/Accident" mislabel and the Topple-vs-Shock inconsistency.
- `Incident.CauseSeq/CauseKind` point at the ledger event that actually caused the death (strike, dose, arm, fire, trigger). `CauseClock/CauseRoom` are derived from it, which fixes wrong "when/where" answers for every delayed method.
- New `DamageType` values `Cold, Gas, Toxin` are appended. They are bloodless via `PhysApi.IsBloodless`, used by Combat and by Gore (coordinate with corpse-discovery).

### A7 Determinism, saves, cost
- `Phys.Tick` runs when `S.Tick % 10 == 0`. It pops due timers (by DueClock, then Seq), then advances only the active processes: fires, gas rooms, doses, held-underwater, drying patches that are still wet. Cooling and drying are **lazy**: they are evaluated when queried.
- `Body.TodShift` becomes a *derived cache* that Thermal writes whenever a body's segment changes: apparent − true death clock, solved from the cooling curve and the effective-minute rigor/livor. `Evidences` therefore keeps reading `DeathClock + TodShift` unchanged. The one-shot writes (SetPieces `SP_Release`, `OnColdHide`) are deleted in migration, and heat/cold tricks become real dwell-time effects.
- The flag families migrate to typed state one at a time, each keeping a read adapter until Phase 4:
  1. stoked, furnace, fire
  2. wet, wettrail, wetsleeve
  3. frost
  4. sedate, drowsy
  5. breaker, breakerreset
  6. pushed, pillow, nails, scratch
  7. sealed, sealedat
  8. silence, roar
- **Continuation prerequisites** (MAP 5, fixed before anything relies on saves; coordinate with time-on-demand):
  - Set `_lastMinute` from `Clock` in `FromState`.
  - Rebuild `Knowledge.Open`, or key it by the stored target.
  - Move the door close/relock queues into `Phys.Timers`.
  - Drain or save `_convos`.
  - Proof: `lab cont` stays IDENTICAL.
- **Save size:** Phys ≤60 KB per day in typical play. There are no histories beyond ≤8 thermal segments per body and patch aggregates. Presentation polls state, so a lost `S.Out` event after a long skip is harmless (M1).

### A8 Events, ledger, traces
- **`GameEventType.Phys`** is the one new type, appended. Key = PhysFx, Id = furniture/door/fixture, Actor/Target, Pos, Value = magnitude, Data = invariant `k=v;` args (path points, pivot, height, level). The PhysFx keys:

  | Group | Keys |
  |---|---|
  | Falling and impact | `fall` `tumble` `topple` `drop` `crash` `shatter` |
  | Water | `splash` `submerge` `float` `sink` `drain` `drip` |
  | Fire, heat and cold | `ignite` `burn` `extinguish` `smoke` `steam` `frost` `melt` |
  | Electricity | `spark` `trip` |
  | Doors, links and mechanisms | `bolt` `thread` `cut` `release` `swing` `ring` `trapdoor` `sandbag` `stopclock` `play` |
  | Moving bodies | `drag` `carry` `cart` |
  | Air | `gas` |

- **Ledger:** typed constants in `PhysLedger`, non-routine, with `Plan` set whenever the event is part of a plan so that Housekeeping keeps them for replays: PhysFall, PhysTopple, PhysRelease, PhysIgnite, PhysBurnt, PhysSubmerge, PhysShock, BreakerTrip, PhysGas, PhysMelt, PhysTimer, PhysLink, PhysDose, PhysDrag, PhysHazardCapped.
- **`TraceKinds*.cs` registry:** maps the id (existing `Trace.Type` strings are kept) → Game decal id, exam LineBank key, ClueTags, persistence (dries / cools / never), default visibility and merge radius.
  - New kinds: DragTrail, DripTrail, WetPrint, SoilPrint, FlourPrint, AshPrint, WaxDrip, ThreadFiber, PinHole, KeyScrape, RopeScrape, DustOutline, Puddle, Condensation, Frost, Soot, Residue, Vomit, CableStrip, Shavings, Splinter, ShardSpray, Sawdust, BoneDust, BloodHaze, Smell, CandleOut.
  - Scorch, PowderSpill, KeySlide, Debris and DrainBlood finally get real decals.

### A9 Game mirror contract (`Game/Physics`, implementer 4)
| Kernel cue | Game presentation | Always ends at | Budget |
|---|---|---|---|
| Death, Collapse, `fall`/`tumble` | Ragdoll built from CharacterJoints on `ActorRig.SetBodyColliders` shapes. It is guided along the kernel path (from → via → to, height), then blended within 0.4 s into the kernel `RestPose` (Pose + DeadVariant + Yaw) | ≤0.3 m from kernel `Pos` (Gore decals anchor there); frozen before `DiscoveryFilm` | ≤3 simulating (hard cap 4); freeze on sleep or after 3 s; off-screen → snap |
| `carry` / `drag` / `cart` | Shoulder carry (as now), Drag pose with trailing ragdoll legs, or a pushed Cart with the body under a sheet. Drips appear only where the kernel laid trail traces | kernel carrier position | 1 partial ragdoll |
| `topple` | Rotation about the kernel pivot edge (0.6–0.9 s ease-in, 3° bounce), dust puff; contents thrown then snapped | kernel furniture pose (`PhysApi.PoseOf`) | — |
| `drop`/`crash`/`shatter` | Rigidbody drop from the kernel height; `PropMaterial` shatter for Brittle items; kernel fragments are items | kernel item support | ≤40 debris, auto-despawn |
| Fixtures: `release` (chandelier/sandbag), `trapdoor`, `ring`, `bolt`, `thread`, `stopclock`, `play`, press, drain | `FixtureViews`: rope whip and cleat spin, bell swing, bolt slide, thread taut → slack → pulled out, hands stop, needle arm, water level lowers | kernel fixture state | — |
| `splash`/`submerge`/`float`/`sink` | Surface ripples + splash particles; the body floats face-down or rests on the bottom as the kernel says; blood-haze tint from the kernel level; wet sheen that dries on the kernel schedule | kernel water state | shader only, no fluid sim |
| `ignite`/`burn`/`smoke`/`steam`/`frost`/`spark`/`gas` | Flames scaled by the kernel level; smoke/steam/haze particles; frost decals and breath fog in the cold store; spark bursts + light flicker; char/ash material swap; soot decals. Also fixes the duplicated `StokeFire` light | kernel levels | particle pools |
| ItemState | blood, washed, wet, burnt, charred, melted, broken, soil, frozen → `ItemView` materials | kernel item | — |
| Any Rigidbody collision | Impact audio by material pair (cosmetic) | — | pooled |
| Replay segment | `ReplayStage` re-enacts Phys ledger events of the segment and shows only traces with `Trace.Clock ≤ t`, so the aftermath is historically correct | ledger | — |

Rules on the Game side:
- **No write-back** (`PhysicsGrab.cs`, `ItemView.cs`):
  - Drop the write-backs nobody caused (`by == null`), NPC-capsule shoves and settling jitter.
  - Take the break floor from world height, not from the player.
  - Player grabs, throws and pushes go through `sim.PlayerPhys(PhysAction)` only.
  - Items in the room of an active incident stay kinematic until the player deliberately grabs them.
- **Layers** (`PhysicsLayers.cs`): 8 Hitbox (queries only), 9 ActorBody, 10 Ragdoll, 11 PropDynamic, 12 Debris. Apply `IgnoreLayerCollision` at boot. Set `Time.maximumDeltaTime = 0.1` at runtime. Never edit ProjectSettings while other agents run builds.
- **Frame budget:** physics ≤1.5 ms average and ≤3 ms p95 per frame including ragdolls, on the reference laptop. `AutoProbe.Murder` records the physics counters.

### A10 The player uses the same rules (non-lethal by H5)
- **What the player can do:**
  - light, stoke or put out a fireplace (it warms the room and any body);
  - throw things into the pool (they sink or float, and blood washes off);
  - open the cold store (fog pours out);
  - burn paper in a lit incinerator;
  - pour water;
  - knock things off shelves (they break and leave a dust outline);
  - topple chairs;
  - ring the bell;
  - trip or reset a breaker;
  - wind or stop a clock;
  - play the recorder or gramophone;
  - carry a candle into the wine cellar;
  - set down ice and watch it melt.
- **Re-enactment (재현)** — during an investigation the player can test a physical claim in place:
  - Examples: how long a candle takes to burn to the thread; whether an ice block melts in the cold store in 40 min; whether a thread fits under this door; whether the rope reaches the cleat.
  - `PhysApi.WhatIf(query)` evaluates the closed-form rules without changing the world, and the result is filed as an *Experiment* finding.
  - This is an exposing-path channel (§D3) and a ready hook for the future trial. It is BASSLINE's own mechanic, not a DR one.

---

## B. Murder-act composition (`Sim/Murder`, implementer 2)

### B1 The model
```
Act = Approach → [Subdue] → Mechanism(Agent) → PostKill { Disposal*, Staging* }  +  TrickRuns (§C)  +  Drama (§C4)
```
- **`ActRecord`** is stored in `GameState.Mur.Acts`, and `MurderPlan.Act` holds its id. Fields:
  - Id, Plan, Culprit, Victim, IntendedVictim;
  - Approach, Subdue, Mechanism, Agent (item id or environment ref);
  - Disposals[], Stagings[], TrickRuns[];
  - Archetype, Aha, Scheme, Roles[] (Helper, Protector, Scapegoat, SecondActor);
  - KillRoom, StageRoom, DumpRoom, Tier (D1–D3), Signature, CauseSeq, State, Log.
- `MurderPlan.Grammar` is kept only as a derived **display label** (e.g. `Lure→Drown+ColdShift`) for old consumers until Phase 4. New code never parses it.
- **Step registry.** Every `PlanStep.Kind` used by acts, tricks, disposals and stagings is a registered `StepDef`:
  ```csharp
  StepDef {
    Kind, Stage, Motion (Anim + ActionAnim), Hot, Lethal,
    Think(ctx) → Activity, Exec(sim, actor, ActionStep) → bool,
    Duration, Noise, ExposureSec, CaptionKey, Beat (foreshadow)
  }
  ```
  - `Crime.Think/Exec` and `Movement.StepMotion` ask `Steps` first. An unknown kind **faults loudly**; it no longer advances silently.
  - `Stage` is one of Prep, Approach, Subdue, Kill, PostKill, Stage, Dispose, Alibi, Retrieve.
  - `MurderApi.IsHotStage(plan)` is true when the current step is in Subdue, Kill, PostKill, Stage or Dispose, or in an armed Approach. time-on-demand uses it instead of hard-coded names.
  - One `IsLethalStep` replaces the six divergent "lethal step" lists: Crime.Lethal, the Cases abort list, Testimony's lie list and the three Augment passes.

### B2 The composer (`Composer.cs`, `Planner.cs`)
1. **Motive.** Take `Relations.Pressure` (target, motive), plus live **schemes** (§C4) and Yusti's rule pressure (CH09 and similar). The non-violent alternatives still come first, as today.
2. **Culprit style and skills.** Each cast id has a `StyleDef` registry entry (written by the 'planner' content author):
   - Style axes: Meticulous, Theatrical, Practical, Impulsive, Technical.
   - Skill tags: Tech, Electric, Clocks, Chem, Cooking, Theatre, Voice, Sound, Craft, Strength, Social, Records, Bodies, Cold, Photo, Outfits, Swim.
   - Comfort rooms and taboo tags (e.g. "never fire", "never dismember").
   - Defaults come from `CastDef` stats, job, hobbies and FavRooms:

     | Resident | Skills |
     |---|---|
     | 윤해린 | Tech/Electric/Clocks |
     | 강준서 | Cooking/Strength |
     | 차은결 | Bodies/Cold/Chem |
     | 차도윤 | Craft/Chem, Meticulous |
     | 문재하 | Theatre/Voice |
     | 서라온 | Sound |
     | 남가온 | Records |
     | 신채령 | Outfits |
     | 임민서 | Strength/tools |
     | 송예담 | Photo/Theatre |
     | 김진우 | Social/perception |
     | 백이현 | Social/helpers |
     | 권태겸 | keys/logistics |
     | 한서윤 | schedules/Records |
     | 오수아 | Theatre/charm |
     | 유시온 | Impulsive/Sound |
     | 정세나 | Swim/timing |

   - Infer, Composure, Deceit and Obs gate the tier.
3. **Opportunity index.** Built at loop start and updated on knowledge events. For each culprit it covers only what they know:
   - rooms, items and fixtures (`ItemSeen`, `visited:` facts, published house schedules);
   - the victim's **routine windows** from habits they have observed (swims at 7, thermos at bedtime, reads in the library at night);
   - this loop's powers;
   - active chapter rules (CH03 outages, CH16 closures, CH22 noise, CH23 dark).
4. **Candidates**, amortised at ≤64 per tick. Take Approach × Mechanism × Agent, pruned by preconditions and knowledge. `TrickApi.Options` then attaches 1–3 tricks, exactly one flagged as the aha, plus the disposals and stagings those tricks need.
5. **Score** = Fit (style, skills, comfort rooms) + Drama (aha strength, archetype freshness, relationship reframe available) + Safety (exposure, noise vs masking, abort risk) + Variety (penalties for the same mechanism, aha, room or archetype this campaign; the previous chapter's archetype is a **hard ban**) + jitter (`Stream.Murder`).
6. **Admission** (hard filters):
   - budget reservation;
   - H5 mechanism registered;
   - `FairPlay.Predict` finds ≥2 paths per falsification *after* the planned concealment;
   - ≤2 power dependencies and ≤2 house-rule dependencies;
   - within the chapter's tier cap;
   - every step has a motion.

   A candidate that fails moves on to the next. If none passes, fall back to a non-lethal alternative or wait.
7. **Emit.** Produce PlanSteps from the defs, ordered by Stage. Prep steps can run in daily life hours earlier; these are the foreshadow beats. Register the TrickRuns and open the CaseFile (intended version).
8. **Execute** through `Steps`. A failed step → Replan (same act, new opening), at most 3 times → then Abort, which returns the reservation. After the kill, Abort is replaced by Improvise.
9. **Improvise.** This runs after the kill, and always after a crime of passion (`A.Impulse`). Pick ≤2 **quick tricks** doable within 45 min from what is at hand:
   - move body + accident staging;
   - key return;
   - weapon back on display;
   - stop the clock;
   - plant a frame item;
   - heat or cold shift if a source is near.

   `FairPlay.Predict` runs as for a planned act. This guarantees ≥1 trick and produces the "panic cover-up" archetype.

**Tiers by chapter:**

| Chapter | Tier | Limits |
|---|---|---|
| Ch1 | D1 | aha + ≤1 support |
| Ch2 | D2 | ≤2 falsified axes |
| Ch3+ | D3 | ≤3 tricks; schemes, double events and protectors allowed |

### B3 Def schemas (`Sim/Murder/Catalog`, loaded by `Catalog.Build()` then `Catalog.Validate()`)
**Common header** (every def has it):
- Id, Kor (LineBank key), Tags;
- Requires[] / Excludes[] (tags such as `room:Pool`, `item.tag:cord`, `fixture:Chandelier`, `power:Echo`, `mech:bloodless`, `body:in-place`, `moves-body`, `staged:accident`);
- Steps(ctx) → PlanStep[], Motions[];
- Noise {Silent, Low, Mid, Loud}, ExposureSec;
- Seams[] (TraceKind/ClueTag), Failures[], Tier, Status (existing | new).

**Per-family fields:**

| Def | Adds |
|---|---|
| `ApproachDef` | isolation rule, rendezvous room choice, invitation text key, refusal branch |
| `SubdueDef` | success test (strength, surprise, dose), duration, resist noise |
| `MechanismDef` | Bundle (6), ResultType (4), HazardKind/DamageType, Bloodless, vital flags, struggle profile (duration, defence wounds, scratch on the culprit), PhysPreconds (e.g. `victim.InWater && (held‖KO)`), CauseTimeRule (contact, dose, arm, trigger), wound signature, signature ledger event |
| `AgentDef` | ItemDef template (new types go through `ItemCatalog.Register`), PhysProfile overrides, Spawns (room types, count, home spot), conceal class, vanish options, swap-compatible edges |
| `DisposalDef` | target (Weapon, Clothes, Body, Part, Document), place, rule invoked, what survives |
| `StagingDef` | presented cause, setup, the vital flags and seams that contradict it |
| `RoomDef` | RoomType → ambient, humidity, noise mask, fixtures, published rules, signature entries, props needed |
| `SchemeDef` | a non-lethal plan by the victim or a third party that the composer may hijack |
| `StyleDef` | as in B2 |

**Registries are split per family file:**
```csharp
static partial class Catalog {
  static readonly int _k = Reg.Mechanisms("Water", new MechanismDef { … }, …);
}
```
- `Reg` appends entries; `Catalog.All` sorts by (family, id, Ordinal). Deterministic, no reflection.
- New item and furniture **types** register through the hooks `ItemCatalog.Register`/`FurnitureCatalog.Register` added once to `WorldTypes.cs` (implementer 1). Family files never edit `WorldTypes.cs`.
- `Catalog.Validate()` runs at boot and in SimTests. It checks:
  - unique ids;
  - LineBank keys exist;
  - motions and step kinds are registered;
  - ≥2 path templates, seams and failures are present;
  - Requires tags resolve;
  - the Korean lint passes (banned jargon, particle check).

  An invalid entry is excluded and logged; it never crashes the game.

### B4 Catalogue (seed ids; content authors fill details to the targets)
Bundles: FC 대면 충돌 · OR 물체 위험 · DR 지연된 결과 · MI 신분 오인 · HC 은닉·변조 · IP 정보 압박. Results: I 즉시 · S 단기 지연 · L 장기 지연 · C 조건 충족.

**Approaches (14)**

| Id | Description |
|---|---|
| `A.Ambush` | stalk until the victim is alone |
| `A.LureNote` | invitation to a secluded room |
| `A.LureFavour` | "help me carry this to the cellar" |
| `A.RoutineIntercept` | wait at the victim's habit spot and time |
| `A.NightVisit` | knock at night |
| `A.GatheringSlip` | slip out of a gathering |
| `A.Blackout` | breaker, CH03 or CH23 |
| `A.Disguised` | mask, cloak, raincoat, the Guise power |
| `A.Courier` | an unwitting helper delivers the note |
| `A.VictimSummons` | the victim's own scheme brings them together |
| `A.CryForHelp` | a recorded or echoed cry draws the victim into a room |
| `A.Escort` | walk a drowsy victim "to bed" |
| `A.Impulse` | an argument boils over; forces Improvise |
| `A.Remote` | no meeting: trap, dose or rig |

**Subdue (9):** `S.None` · `S.Surprise` (from behind) · `S.StunBlow` · `S.Sedate` (drink) · `S.Liquor` (decanter, potentiates) · `S.Gas` (closed room) · `S.Restrain` (rope/cord) · `S.LockIn` (cold store, cellar, chest, darkroom: wedged) · `S.Jolt` (non-lethal shock, push off balance).

**Lethal mechanisms (40 variants; bold = new)**

| Id | B/R | Agent(s) | Physical preconditions | Motion beats | Noise · exposure | Seams |
|---|---|---|---|---|---|---|
| M.Blunt.Head | FC/I | Candlestick, Statuette, Bookend, Decanter, Wrench, Hammer, Trophy, Iron, RollingPin, FryingPan, PipeSection, Crowbar, Bottle, Vase, CueStick, **FrozenJoint** | reach, victim unaware or KO | stalk → Overhead → strike → ragdoll fall | Loud · 3 s | impact spatter + cast-off (Gore), dent matches agent mass/edge, blood on agent |
| M.Stab | FC/I‖S | KitchenKnife, Chisel, Scissors, LetterOpener, IcePick, FirePoker, **HatPin, Icicle, TrophySpear** | reach | Stab ×1–3 → Stagger/Crawl | Mid (scream) | wound width = blade, arterial spray on neck/chest |
| M.Slash | FC/I‖S | Cleaver, GardenShears, Scalpel, PaletteKnife, SkinningKnife, Fragment | reach | Slash → defensive raise | Mid | defence cuts on forearms |
| **M.Impale.Fall** | OR/I | fire-iron stand, antler rack, stage spear | victim at an edge above a spike | Shove → fall | Loud | wound angle ≠ a thrust |
| **M.Projectile** | OR/C | spring **TrophyCrossbow** on a door string | victim opens a known door | Tie (string through keyhole/transom) | Mid snap | empty bracket on the trophy wall, string scrape |
| M.Strangle.Cord | FC/I | Rope, Scarf, CurtainCord, ExtensionCord | from behind; Strength ≥ victim, or victim KO | Strangle 30–60 s + struggle | Low | horizontal ligature, nail-scrape, scratch on culprit's hand |
| **M.Garrote.Wire** | FC/I | PianoWire | same | Strangle 20 s | Low | incised ligature, missing piano string, cut glove |
| **M.Hang** | FC/I | Rope/CurtainCord over the gallery rail, fly bar or beam | victim KO/sedated; hoist by Strength or pulley | Lift → Pull → Tie | Low | **rising ligature angle**, rope scrape on the rail, no drop marks |
| M.Smother | FC/I | Pillow | victim asleep/sedated | press 60–120 s | Silent | petechiae, pressed pillow, fibres in the mouth |
| M.Drown.Pool | FC/I | PoolWater | victim in the water, or at the edge and held/KO | grab → hold under 40–90 s | Loud splash | clothed drowning, froth, wet sleeves, splash area |
| **M.Drown.Shallow** | FC/I | ShallowWater (MirrorWater) | victim KO face-down | lay → hold | Low | froth, "drowned in 15 cm", dry back |
| **M.Drown.Tank** | FC/I | Aquarium (GrandHall/ButlerRoom) | held at the tank lip | hold | Mid | sand/weed in the mouth, decor stones displaced, floor splash |
| **M.Drown.Cask** | FC/I | Barrel, Washer drum, Sink | held | hold | Mid | wine froth, stained collar |
| **M.Airless** | DR/S | Chest, Wardrobe, ColdLocker, Crates | sedated; container airtight and latched | Carry → Lift → Close | Silent | nail scratches under the lid, fibre on the latch |
| **M.Gas.CO** | DR/S‖C | damper shut, rag in the boiler flue, brazier | closed room, victim asleep/sedated | stuff rag, close damper | Silent | pink lividity, rag, soot, earlier headaches |
| **M.Gas.Cellar** | DR/C | fermenting casks | lured down; door **wedged** | wedge | Silent | candle goes out, wedge splinter |
| **M.Gas.Fume** | DR/S | darkroom bottles mixed | door wedged | pour | Silent | bitter smell, stained tray, missing bottles |
| M.Fall.Stairs | FC/I | stair top | victim at the top, no one awake within 10 m | Shove | Loud | palm bruise, tumble bruises, scuff, torn button |
| M.Fall.Rail | FC/I | gallery rail over a Void | same | Shove | Loud | rail dust wiped, 4.8 m injuries |
| **M.Fall.Trapdoor** | OR/C | stage trapdoor latch (link) | victim on the mark ("rehearse with me") | Pull | Loud | latch fibre, greased hinge, under-stage props |
| M.Trip.Wire | OR/C | fishing line on the stairs | victim's routine descent | Tie ×2 | Loud | line residue, knot marks on balusters |
| M.Crush.Topple | OR/C‖I | Bookshelf, Wardrobe, ClockCase, Clock, Statue | victim under the reach; rope/wire pull or shove | Tie → Pull | Loud | rope scrape on the rear top edge, wall bracket loosened, crushed footprint |
| **M.Crush.Chandelier** | OR/C | chandelier fixture; rope freed at the cleat (pull, cut, candle burn-through) | victim under it (lured seat, routine seat) | Tie thread + candle / Release | Loud | **wax on the cleat**, burnt thread end, clean cut vs fray, glass spray |
| **M.Crush.Drop** | OR/I | Statuette, Bookend, planter pot from the gallery | victim below the Void | Lift → Drop | Loud | empty home shelf, rail dust, impact from above |
| **M.Crush.Sandbag** | OR/C | fly bar sandbag pin (link) | victim on the stage mark | Pull | Loud | pin fibre, moved counterweight |
| M.Crush.Press | OR/L | Press | victim KO | StunBlow → Carry → Arm | Machine | press log, circuit 7, carry trail |
| M.Shock.Device | OR/C | Washer, Terminal, Jukebox, Arcade, FilmProjector, FloorLamp | circuit on; stripped cable; wet floor; victim uses it | Strip (Pliers) → Pour | Mid crack | stripped cable, shavings, breaker minute, palm burn |
| **M.Shock.Water** | OR/C‖I | powered lamp on an ExtensionCord into the pool or washer | victim in the water | Carry cord → Drop | Loud | breaker minute, cord on the deck, scorched plug |
| **M.Shock.Handle** | OR/C | energised handle or rail + wet mat | victim opens the door | Strip → Hide cable under the rug | Mid | palm mark, cable under the rug |
| M.Poison.Meal | DR/S | Foxglove, PoisonVial, DevChemical, **Belladonna** | one cup or seat at a meal (seating order, handedness) | Serve/Pour | Silent | cup residue, symptoms at the table, who served |
| M.Poison.Personal | DR/L | personal Thermos, Tea, Candy, gift | victim's room empty and unlocked | Plant | Silent | powder spill, entry seen, self-locked room |
| **M.Poison.Contact** | DR/S | needle in a glove, pen, thimble or piano key | victim's routine use | Plant/Swap | Silent | fingertip puncture, swapped object |
| **M.Poison.Delayed** | DR/L | **PaleMushroom** (false recovery) | shared lunch cooked by the culprit | Cook/Serve | Silent | vomit patch in the afternoon, recovery witnessed, death at night |
| **M.Overdose** | DR/S | Sedative, **PainAmpoule** + liquor | drink | Pour | Silent | empty ampoules, glass residue |
| **M.Allergen** | DR/S | the victim's allergen (from CharacterBible) + **hidden EpiKit** | known allergy | Serve + Hide kit | Silent | kit missing (dust outline), allergen in the dish |
| **M.Envenom** | OR/C | aquarium spined fish, **FishNet** | victim feeds the fish, or is lured to | Lure/Place | Low | sting swelling, wet net, warning plate |
| **M.Hypothermia** | DR/L | ColdStorage + wedge + sedation | sedated or wedged in | Wedge/Close | Silent | frost, scratches inside the door, wedge splinter |
| **M.Burn** | OR/S | incinerator room, or bed fire (candle + **LampOil**) | victim KO | Pour → Ignite | Mid | **soot in the airway**, oil smell, candle stub |
| **M.Scald** | OR/C | boiler steam valve | victim at the gauge (maintenance routine) | Turn valve | Loud hiss | valve wheel turned, burn pattern |
| **M.Bleed.Flee** | FC/S | Stab/Slash, then the victim flees and locks himself in | victim can still move | Stab → victim runs | Mid | blood trail, key in a bloody hand |

**Agents (≥70; + = new type)**

| Group | Agents |
|---|---|
| Blunt | Candlestick, Statuette, Bookend, Decanter, Wrench, Hammer, Trophy, Iron, RollingPin, FryingPan, PipeSection, Crowbar, Bottle, Vase, CueStick, Book, +FrozenJoint, +IronDoorstop |
| Blade/point | KitchenKnife, Cleaver, Chisel, Scissors, LetterOpener, IcePick, FirePoker, GardenShears, Scalpel, PaletteKnife, SkinningKnife, Fragment, +HatPin, +SewingNeedle, +Icicle, +TrophySpear, +TrophyCrossbow |
| Cords | Rope, Scarf, CurtainCord, PianoWire, ExtensionCord, Tripwire, Thread, +BellRope (fixture) |
| Substances (≥6 poisons) | Foxglove/PoisonVial (digitalis: slow heart, 30–60 min), DevChemical (darkroom: fast, bitter), Sedative, +Belladonna (agitation, wide pupils, 60–90 min), +PaleMushroom (6–10 h, false recovery), +RatPoison (3–5 h), +PainAmpoule, +Allergen (per victim), +FishVenom (sting), liquor (Decanter/Bottle/Beer, potentiator), +Syringe (vector) |
| Heat/fire | +Matches, +Candle, Candelabra, +LampOil, +Brazier, Iron; env: Fireplace, Stove, Incinerator, Boiler |
| Cold/water | +IceBlock, Icicle; env: ColdStorage/ColdLocker, Fridge, PoolWater, ShallowWater, Aquarium, Barrel, Washer, Sink, Bucket |
| Electric | ExtensionCord, Pliers, FloorLamp, devices, Switchboard |
| Falling/machines | env: Chandelier, Bookshelf, Wardrobe, ClockCase, Statue, +Sandbag, +Trapdoor, GalleryRail, StairTop, Press |
| Timers/sound/record | Thread, +Wedge, +Tape, Recorder, Gramophone + +Record, Clock, +PocketWatch, +WindingKey, Camera (instant photo) |
| Identity | TheaterMask, Cloak, Raincoat, SpareApron, +Wig, +Gloves, borrowed outfits, Button (planted), Document/Envelope/Invitation/Notebook (forgery) |
| Transport/containers | Sheet, Cart, Trolley, TeaCart, +LaundryBasket, Chest, Crates, Barrel |
| Cutting/cleaning | Hacksaw, BoneSaw, Towel, Bucket, Mop, Bleach |

Poison names stay folk or fictional. The data holds only game timelines (onset, peak, lethal clock, signs, rescue window), never real doses or recipes.

**Disposals (17)**

| Id | What it does | What survives (seam) |
|---|---|---|
| `D.Burn.Incinerator` | burn | metal, buttons, bone; furnace warm 2 h; scheduled 22:00 burn |
| `D.Burn.Fireplace` | small burn | half-burnt scraps, ash |
| `D.Sink.Pool` | sink or float | the filter catches floaters at 06:00 |
| `D.Sink.Aquarium` | hide among the decor | fish disturbed, stones moved |
| `D.Sink.Cask` | into a wine cask | wine-stained |
| `D.Bury.Greenhouse` | bury | soil prints, dug planter |
| `D.Stash.Cold` | cold store | frost |
| `D.Wash.Laundry` | wash in the washer | washer run time, residue for Afterglow |
| `D.Return.Display` | wash and put back | **dust outline mismatch**, still wet or cold |
| `D.PlainSight` | among an identical set | the pair no longer matches |
| `D.Compartment` | ClockCase, Dollhouse, Piano, Organ, Altar | fresh scratches |
| `D.Drop.Void` | drop from the gallery | hall impact mark |
| `D.Dismember.Scatter` | `Gore.Dismember`, gated by a motive (§C4) | saw marks, drain blood, bone dust |
| `D.Melt` | ice weapon melts | puddle out of place |
| `D.Eat` | FrozenJoint cooked and served | missing joint, cooking log |
| `D.Dismantle` | unscrew the cue | wrong half in the rack |
| `D.ElevatorPit` | via the machine-room hatch | hatch dust, grease |

**Stagings (21)**

| Group | Stagings |
|---|---|
| Accident | `G.Acc.Fall`, `G.Acc.Topple`, `G.Acc.Chandelier` (frayed rope), `G.Acc.Shock` (faulty appliance), `G.Acc.Drown` (swim cramp), `G.Acc.Fire` (candle to bed/rug), `G.Acc.Cold` (door swung shut), `G.Acc.Gas` (flue blocked "by debris"), `G.Acc.Animal` (fish sting) |
| Suicide | `G.Sui.Note` (forged note + poison), `G.Sui.Hang`, `G.Sui.Cut` (no hesitation marks), `G.Sui.Leap` |
| Natural death | `G.Nat.Heart` (digitalis), `G.Nat.Sleep` (sedate + smother), `G.Nat.Allergy` |
| Other | `G.Ritual` (house-lore scene: candle ring, wax seal, "the house took him"; every claim refutable), `G.Frame.Struggle`, `G.Frame.Item` (planted button, hairpin, glove), `G.Message` (fake dying message), `G.SecondWound` (post-mortem fall, drowning or burning over the true wound) |

Each staging lists the vital flags or seams that contradict it: no soot, no froth, a rising ligature angle, no hesitation marks, petechiae, a push bruise, ooze instead of bleeding.

### B5 Rooms as murder machines (`RoomDef`; rooms marked * appear in some layouts only)
| Room | Signature mechanisms | Signature tricks | Disposals | Fixtures / props (+ needed) | Published rule |
|---|---|---|---|---|---|
| Pool + WaterRoom | Drown.Pool, Shock.Water | T.NightLockHelper, T.HeatShift (28 °C water) | Sink.Pool | filter, pump, drain valve, diving board | open 08–21, night lock 22:00, filter 06:00 |
| Incinerator | Burn | T.ScheduledBurn, T.HeatShift | Burn.Incinerator | furnace door, ash pit | burn at 22:00; furnace warm 2 h |
| ColdStorage | Hypothermia, Stab (Icicle) | T.ColdShift, T.IceWeapon, T.IceTimer | Stash.Cold, Melt | spring door (opens from outside), +IceBlock rack | 2 °C; door swings shut |
| BoilerRoom* | Gas.CO, Scald | T.NoiseMask, T.HeatShift | — | flue, valve, gauge | roar 23:00–01:00 |
| MachineRoom / PowerRoom | Crush.Press, Shock.* | T.OutageClock, T.BreakerAnchor | ElevatorPit | switchboard log, hatch | resets after 12 min; electric clocks stop |
| ClockMuseum* | — | T.ClockSet, T.StoppedWatch | Compartment (ClockCase) | +WindingKey | "no two clocks agree" (each clock's offset is public) |
| MirrorWater* | Drown.Shallow | T.MirrorWitness (the reflection flips handedness) | — | shallow pool | ceiling reflection |
| RainCorridor* | — | T.RainExplains (wet explained, noise), footprints washed | — | rain frames | everyone passing gets wet |
| Theater (+ Wardrobe suite) | Fall.Trapdoor, Crush.Sandbag, Hang (fly bar) | T.Disguise, T.SecondDoor | — | +Trapdoor, +FlyBar/Sandbag, costumes, +Wig | rehearsal shows the machinery; night lock |
| EmptyAuditorium* | — | T.RecordedVoice (broadcast speakers) | — | speakers | broken broadcast plays at the hour |
| WaitingRoom* | — | T.BellAnchor (departure bell), T.OutageClock (electric board) | — | ticket bell, board | departures announced |
| WhiteDoors* | — | T.HeightTell (low lintel: a hair trace, stoop) | — | doors of different heights | — |
| Chapel | Burn (candles), Gas.CO (brazier) | T.CandleTimer, T.BellAnchor, G.Ritual | Compartment (Altar) | +bell rope, candles, +Brazier | bell 07:00 and 22:00 |
| Greenhouse | Poison.* (Foxglove, Belladonna, PaleMushroom) | T.SoilPrints | Bury.Greenhouse | planters | humid: slow drying |
| Darkroom | Gas.Fume, Poison (DevChemical) | T.PhotoAlibi (instant photo with a fast clock in frame) | — | trays, safelight | light 0.22 |
| MusicRoom | Garrote.Wire | T.RecordedPerformance | Compartment (Piano) | piano, gramophone, +Record | — |
| Library / Archive / Study | Crush.Topple, Blunt (Bookend), Poison.Contact (pen) | T.DustOutline, T.Forgery (handwriting samples in Archive) | PlainSight (hollow book) | dusty shelves, loan ledger | — |
| GrandHall + Landing + Stairwell | Fall.Rail, Fall.Stairs, Crush.Drop, Crush.Chandelier, Trip.Wire, Drown.Tank, Envenom | T.CandleTimer (chandelier), T.SeenAlive | Drop.Void, Sink.Aquarium | chandelier winch/cleat, rail segments, aquarium | chandeliers lowered at dusk; fish warning plate |
| Kitchen + Dining | Poison.Meal/Delayed, Allergen, Stab | T.SeatSwap, T.EatTheWeapon | Eat | stove, fridge, flour (prints), +FrozenJoint | night lock; meal times |
| Laundry | Shock.Device (Washer), Drown.Cask (drum) | T.LaundryCart (body moved unseen) | Wash.Laundry | washer, iron, +LaundryBasket | washer run log |
| Lounge / Parlor* | Blunt (Decanter), Stab (IcePick), Strangle (CurtainCord) | T.RecordedVoice (jukebox), T.HeatShift (fireplace) | Burn.Fireplace | fireplace, bar, jukebox | — |
| GameRoom | Blunt (CueStick), Shock (Arcade) | T.ChalkTell (blue chalk), T.Dismantle | Dismantle | pool table, arcade | — |
| TrophyRoom* | Projectile, Impale.Fall, Slash (SkinningKnife) | T.SecondWound | Compartment (StuffedBeast) | +TrophyCrossbow bracket, antlers | "the crossbow is sprung, keep clear" |
| DollRoom* | — | T.Dummy (seen at a distance in the dark; Tier 3, with a seam) | Compartment (Dollhouse) | dress forms | — |
| Gallery | Blunt (Statuette), Slash (PaletteKnife) | T.DustOutline, T.PaintTell, CH12 moved exhibits | Return.Display | pedestals | exhibits change |
| Wardrobe | — | T.Disguise, T.BorrowedCoat, T.ThreadBolt (thread, needles) | — | costume racks | — |
| Infirmary | Overdose, Allergen (kit), Slash (Scalpel) | T.MissingRescue | — | med cabinet (lock log), +Syringe, +EpiKit | — |
| WineCellar* | Gas.Cellar, Drown.Cask | T.ColdShift (11 °C), T.CandleTest | Sink.Cask | casks | "take a candle down" |
| TeaRoom* / Bedrooms / GuestRoom* | Smother, Poison.Personal, Airless (wardrobe/chest) | T.ThreadBolt, T.KeyReturn, T.SelfLocked, T.RoomSwap (twin guest rooms) | — | inside bolts, personal drinks | windows do not open |
| Workshop / Storage | Blunt (tools) | T.Wedge (wedges and tape come from here) | Compartment (crates) | saws, lamp oil, rat poison | loan ledger |
| Courtyard* | — | T.ColdShift at night (8 °C) | Bury | fountain (dry), gravel | — |
| Elevator (Yusti only) | — | — | ElevatorPit (from the machine room) | pit | only Yusti rides |

### B6 Migration of existing content (nothing is reverted; old code becomes an adapter, then is deleted in Phase 4)
| Today | Becomes |
|---|---|
| Ambush / Lure / NightVisit / Blackout / Gathering / Disguise | `A.Ambush` / `A.LureNote` / `A.NightVisit` / `A.Blackout` / `A.GatheringSlip` + `T.SealedGathering` / `A.Disguised` + `T.Disguise`, with M.Blunt/M.Stab/M.Slash |
| Press | `S.StunBlow` + `M.Crush.Press` (the press singleton moves into Phys as a device timer) |
| Drown | `M.Drown.Pool` with the R4 precondition (victim in or at the water) |
| Trap Topple / Tripwire | `M.Crush.Topple` / `M.Trip.Wire` (R1/R8, trap lifecycle, nav invalidated) |
| Poison (meal), Bedtime | `M.Poison.Meal`, `M.Poison.Personal` (+`T.SelfLocked`), dose via R7 with `CauseSeq` |
| Strangle | `M.Strangle.Cord` / `M.Garrote.Wire` |
| Push | `M.Fall.Stairs` / `M.Fall.Rail` (R1 fall path; the event carries the path) |
| Shock | `M.Shock.Device` (R5: needs power, a fault and wet contact) |
| Smother | `S.Sedate` + `M.Smother` (+`G.Nat.Sleep`) |
| Recorder, Echo | `T.RecordedVoice` (+ power variant) |
| Silence, Guise, Blur, Fix, Weight | power-backed variants of T.NoiseMask, T.Disguise, the scene-cleaning disposal, T.Wedge (Fix), transport |
| Courier (IG10) | `A.Courier` + role Helper |
| Mutilate | `G.Ritual`/desecration via `Gore.OnPostmortem` |
| Seal / KeySlide | `T.ThreadBolt` / `T.KeyReturn` |
| Tod (heat/cold) + ColdHide | `T.HeatShift` / `T.ColdShift` (dwell-time physics) + `D.Stash.Cold` |
| Message / Swap / FakeNote | `G.Message` / `T.WeaponSwap` / `G.Sui.Note` + `T.Forgery` |
| Noise | `T.NoiseMask` |
| Burn / Dump / Bury | `D.Burn.Incinerator` / `D.Sink.Pool` (R4: sinks or floats, the filter catches) / `D.Bury.Greenhouse` |
| Dismember | `D.Dismember.Scatter` on `Gore.Dismember`, gated by motive (persona affinity only breaks ties) |

The forced proof set (19 kinds) is regenerated from the registries; its signature events are kept as each entry's `SignatureEvent`.

---

## C. Trick grammar and case drama (`Sim/Murder/Tricks`, implementer 3)

### C1 Falsification model
```csharp
public enum Axis { Time, Place, Identity, Cause, Means, Access, Sequence, Possession, Belief }   // append-only
public sealed class TrickDef {                 // Catalog/Tricks_<Axis>.cs
  string Id, Kor; Axis Axis; Axis[] AlsoAxes; string Class15_5; int Tier; string[] FitSkills, Archetypes, Requires, Excludes;
  SetupStep[] Setup;        // ≤5 × {StepKind, When: Prep|PreKill|PostKill|Discovery, Motion, Minutes, Noise, Beat}
  ClaimTemplate Presented;  // typed Prop + LineBank key, e.g. "안에서 잠겨 있었다", "밤 11시 넘어서 숨졌다"
  ClaimTemplate Truth;
  SeamDef[] Seams;          // {TraceKind | Record | BodyFinding, ClueTag, Channel, ProducedBy: setup step | rule}
  PathDef[] Paths;          // ≥2: {channel set, root template, NpcIndependent}
  FailDef[] Failures;       // Interrupted | Witnessed | PhysicsMismatch | FoundEarly | WrongVictim → Abort | Improvise | PartialSeam
  P1451 Why;                // first hypothesis, why plausible, prior clue, conflicting fact, invariant fact, reversing claim key
  string AhaKey; string HingeSeam;   // used when this trick is the case's aha
}
public sealed class TrickRun { string Id, Trick, Plan, Actor; RunState State; /* Planned, SetUp, Active, Triggered, Exposed, Failed */
  Prop Presented, Truth; List<SeamRef> Seams; int PathsVerified; string FailureHit; }
```
- A trick is **only ordinary actions**. Every setup step is a registered `StepDef` with a motion and one ledger event, and no explanation takes more than about 5 physical steps (FP08).
- The presented claim and its seams are **recorded when the step executes** (FP10: every planted falsehood leaves a seam made by the planting itself). They are never re-derived at trial time.

### C2 Composition rules
- **Count.** 1 aha + 0–2 supporting tricks, capped by tier (B2). Target mean ≈2.
  - Supporting tricks must be causally linked to the aha or the mechanism (P1451). For example, T.ColdShift needs T.BodyMove; T.KeyReturn and T.NightLockHelper never appear together.
  - Disposals and stagings are tricks only when they falsify an axis.
- **Compatibility** is declared by tags, not by pass order:
  - `moves-body` ✗ `body:in-place`
  - heat ✗ cold
  - `mech:bloodless` ✗ blood-based stagings (G.Message, a bloody T.WeaponSwap decoy)
  - `staged:X` ✗ a contradicting staging
  - ≤2 power dependencies; ≤2 house-rule dependencies.
- **The aha** must satisfy all of these:
  - (a) it breaks last (L2→L3);
  - (b) its hinge seam is observable before the trial (visibility ≤2 in a reachable room, or re-findable by Experiment);
  - (c) it is rooted in the culprit: `FitSkills ∩ style skills ≠ ∅`, or it uses the culprit's power, relationship or job;
  - (d) it has ≥1 foreshadow beat the player can witness in daily life.
- **Supporting tricks** break first (L1→L2). Where possible they point at a wrong suspect (the scapegoat or the "opportunity suspect").

### C3 Trick templates (44; content authors complete the fields)
Channels: **B** body · **S** scene · **O** object · **R** record · **W** witness · **X** experiment · **P** power. Every row has ≥2 channels, and at least one of them is not W or P.

**Time**

| Id | Setup (physical) | Presented → truth | Hinge / seams | Paths |
|---|---|---|---|---|
| T.HeatShift | keep the body by a fire, the boiler, the incinerator or warm pool water; stoke | died later → earlier | heat-dried side, ash on clothes, wood basket emptied, fire lit out of routine | B·O·X |
| T.ColdShift | cold store, cellar, courtyard or pool, then return | died earlier → later | condensation/frost, drag + drip trail, cold-store door log | B·S·R |
| T.ClockSet | set the room clock before watchless witnesses read it (IG09) | witness time → true time | offset vs official time, dust wiped on the clock face, winding key | O·W·R |
| T.StoppedWatch | break the victim's watch at a false time | 23:10 → 22:20 | no blood under the glass shards, wound age, winding state | O·B |
| T.OutageClock | trip the breaker later to stop the electric clocks | "during the outage" → earlier | two trips in the breaker log, mechanical clocks disagree | R·O |
| T.RecordedVoice | record a voice or scream; timed playback (Recorder, Gramophone, speakers, Echo) | alive/screaming at 23:00 → dead at 22:30 | clip + PlayAt, needle at the record's end, metallic tail | O·W·B |
| T.SeenAlive | walk a dim corridor in the victim's coat; lamp on in the victim's room | seen at 23:00 → dead at 22:00 | shoe size/height prints, damp coat hung back in the wrong order | S·O·W |
| T.CandleTimer | thread at a candle's height; burning through releases a chandelier, sandbag, latch or bolt | nobody near at 22:10 → set at 21:00 | **wax on the anchor**, burnt thread end, stub length | S·O·X |
| T.IceTimer | ice holds a latch, weight or trigger; it melts | same | puddle ring, missing ice (cold-store count) | S·R·X |
| T.SleepTiming | sedate so the victim "went to bed at 10" | died asleep 22–23 → 01:00 | cup residue, stomach stage vs last meal, rigor | B·O |
| T.BellAnchor | ring the chapel or departure bell off schedule | "the bell = 22:00" → 21:40 | fibres on gloves, Yusti's official log | R·W |

**Place**

| Id | Setup (physical) | Presented → truth | Hinge / seams | Paths |
|---|---|---|---|---|
| T.BodyMove | carry, drag, cart or sheet | died in X → died in Y | livor mismatch/double pattern, trail, too little blood at X | B·S |
| T.SceneStaged | smear blood, overturn furniture in another room | "struggle here" → elsewhere | smear not spatter, dry-edged pool, no cast-off | S·B |
| T.RoomSwap | twin guest rooms; swap the door plates | "his own room" → the twin room | palette, wall-clock offset, the key does not fit | O·R |
| T.SecondDoor | use a suite door (Kitchen–Dining, Pool–WaterRoom, Power–Machine, Wardrobe–Theater) (IG04) | "only door was watched" → second door | logger covers one door; flour or wet prints at the other | R·S |
| T.LaundryCart | body under sheets in a laundry cart, trolley or tea cart | "nobody carried anything" → the cart | wheel line with drips, "heavy cart" testimony, fibres in the basket | S·W·O |

**Identity**

| Id | Setup (physical) | Presented → truth | Hinge / seams | Paths |
|---|---|---|---|---|
| T.Disguise | mask, cloak, raincoat or Guise | "a tall masked figure" → culprit | costume returned damp, lintel height, gait | O·S·W |
| T.BorrowedCoat | wear the scapegoat's coat (IG06) | "X was seen" → culprit | loan record, sleeve length, fibre on the culprit | R·O·W |
| T.Forgery | forged note, invitation or farewell (IG02) | "X invited him / he wrote goodbye" → culprit | handwriting vs Archive samples, paper stock from the culprit's room | O·R |
| T.MirrorWitness | act where the gallery sees only the MirrorWater reflection | "left-handed killer" → right-handed culprit | witness position, wound angle | S·B·X |
| T.FrameItem | plant the scapegoat's button, hairpin or glove | "X was here" → culprit | item lacks the scene's contamination; its owner missed it at breakfast | O·W |
| T.FakeMessage | blood letter of the scapegoat's initial | "the victim named X" → culprit | wrong hand/finger, written after death (dry edges) | B·S |

**Cause**

| Id | Setup (physical) | Presented → truth | Hinge / seams | Paths |
|---|---|---|---|---|
| T.StagedAccident | a G.Acc.* setup | accident → murder | vital-flag contradiction + setup seam (cut rope, stripped cable) | B·S·O |
| T.StagedSuicide | a G.Sui.* setup + note | suicide → murder | ligature angle, no hesitation marks, forged note, sedative | B·O |
| T.StagedNatural | a G.Nat.* setup | heart / sleep / allergy → murder | petechiae, residue, missing EpiKit | B·O·R |
| T.SecondWound | post-mortem fall, drowning or burning over the real wound | fell/drowned/burned → earlier wound | bled vs ooze, no froth/soot | B·S |
| T.SelfLocked | delayed poison; the victim locks himself in | "locked, nobody entered" → poisoned earlier | thermos residue, symptoms witnessed, onset timeline | O·B·W |

**Means**

| Id | Setup (physical) | Presented → truth | Hinge / seams | Paths |
|---|---|---|---|---|
| T.IceWeapon | icicle or ice block; melts | no weapon → ice | puddle out of place, low-bleed wound edge, ice count | S·B·R |
| T.WeaponReturn | wash and put back on display | "never moved" → used | **dust outline mismatch**, still wet/cold, Afterglow residue | O·P |
| T.WeaponSwap | plant a decoy with a different edge | decoy → real weapon | wound geometry mismatch, smeared not impact blood | B·O |
| T.EatTheWeapon (D3) | frozen joint → blunt → roasted and served at dinner | no weapon → the roast | cold-store count, cooking log, rounded wound, grease | R·B·W |
| T.Dismantle | unscrew the cue; halves stored apart | "nothing missing" → cue | mismatched halves in the rack, chalk on the wound | O·B |

**Access**

| Id | Setup (physical) | Presented → truth | Hinge / seams | Paths |
|---|---|---|---|---|
| T.ThreadBolt | pull the inside bolt shut with thread under the door | locked inside → outside | fibre on the knob, pin hole, thread end in a pocket | O·S·X |
| T.KeyReturn | lock from outside, slide the key under the door, or plant it during discovery | key inside → returned | key scrape, key position vs gap | S·O·X |
| T.Wedge | wedge or tape the door from outside (or the Fix power) | locked → wedged | splinter/tape residue, workshop wedge missing (loan) | S·R |
| T.NightLockHelper | kill before 22:00 in a night-locked room; Yusti locks it unaware (IG05) | "locked all night" → locked after death | Yusti's lock log, wet trail leaving before 22:00 | R·S·W |
| T.HideInside (D3) | hide in the wardrobe; leave in the discovery commotion | "empty room" → culprit inside | entry order, wardrobe dust disturbed, arrival from the wrong side | W·S |
| T.FirstIn (D3) | victim sedated in a locked room; culprit breaks in first and kills | died hours ago → at the discovery | fresh blood on a cuff, body still warm, entry order | B·W |

**Sequence, possession, belief**

| Id | Setup (physical) | Presented → truth | Hinge / seams | Paths |
|---|---|---|---|---|
| T.TwoStage | a non-lethal first blow or sedation, then a device later | one attack → two | two wound ages, vital differences | B·S |
| T.SealedGathering | slip out of a gathering ≤10 min (toast, suite door) | "at the table all evening" → out | empty seat noticed, chair moved, wet shoes | W·S |
| T.NoiseMask | boiler roar, Silence, jukebox | "no one heard anything" → struggle then | dial off schedule, power residue, Reverb | R·P·S |
| T.PhotoAlibi | instant photo with a fast clock in the frame | "photo proves 21:00" → 21:40 | clock offset in the photo, darkroom log | O·R |
| T.RecordedPerformance | playback of "practice" (piano/bass) | playing at 22:00 → playback | identical slip repeated, needle at the end, instrument cold | W·O |
| T.CourierHelper | an innocent helper relays the invitation (IG10) | "X invited him" → culprit | helper's account, note paper | W·O |
| T.MissingRescue | hide the victim's EpiKit or medicine | bad luck → kit hidden | the kit's dust outline, kit found in a stash | O·S |
| T.SeatSwap | switch seat cards or cups at a meal (poison meant for one place) | random victim → targeted | seat plan vs handedness, cup rim marks | O·W |

**Signature seams worth knowing:**

| Seam | What it proves |
|---|---|
| Dust outline no longer matches | the object was moved and put back |
| Wax on the winch cleat | a candle timer was set at the cleat |
| Rising ligature angle | hanging, not strangling |
| No soot / no froth | the victim was already dead before the fire / water |
| Livor mismatch | the body was moved after livor fixed |
| Two breaker trips | an outage was faked later |
| Needle at the record's end | the record played through to the end (the playback ran) |
| Puddle ring | ice melted there |

### C4 Case drama layer (the Danganronpa-level bar, by structure only)
- **Aha and hinge.** Every case has exactly one aha, a TrickRun whose `HingeSeam` the player can find before the trial. The composer's Drama score rewards ahas that come from the culprit's craft, for example:
  - the engineer's candle timer;
  - the funeral director's cold store;
  - the actor's recorded voice;
  - the stylist's borrowed coat;
  - the cook's roasted weapon.
- **Three layers**, recorded at execution and frozen at `HouseConfirm`:
  - **L1** (first impression) is every presented claim plus what everyone can see at the discovery: a locked door, an "accident" pose, Yusti's official record.
  - **L2** (mid reversal) is what remains after the supporting tricks break: "it was murder, and it points at ___". The suspect is computed as whoever best fits the still-false claims (no alibi in the presented window, owner of the framed item) or is the chosen scapegoat.
  - **L3** is the truth once the aha breaks.
  - A one-trick case gets L2 = "murder by the opportunity suspect". Every layer carries a LineKey the future trial can voice.
- **Victims have plans too.**
  - `SchemeDef` entries: blackmail meeting, prank/scare, trap meant for someone else, theft, confession meeting, self-staging for attention, hiding to eavesdrop.
  - They come from Relations (grudge, fear), goals, CH06 envelopes and Cast secrets (e.g. 백이현 ↔ 권태겸 ledger, 차도윤 ↔ 차은결).
  - Schemes run as visible, non-lethal NPC activities in daily life. The composer may **hijack** them: `A.VictimSummons`, or turn the victim's trap back on its owner.
- **Roles.**
  - *Helper*: unwitting. The courier; whoever lent the coat, decanter or thread; Yusti's night lock.
  - *Protector*: after the fact. Lies, cleans, moves the body, stages. The natural pairs come from Cast secrets, e.g. 차은결 for 차도윤.
  - *Scapegoat*: chosen by grudge relations, a missing alibi, or an item that is available.
  - *SecondActor*: a double event, where A attempts and B kills the same night. Both are recorded, and adjudication follows v2.2: the perpetrator of the earliest ResultTime death.
  - *Wrong victim*: a device or dose kills a non-target → archetype MistakenVictim, with the culprit's shocked reaction animated.
- **Foreshadowing.**
  - `SetupStep.Beat` ∈ {Borrow, Ask (a schedule), Visit (an unusual room), HabitChange, Missing (an object gone), Practice (tests a knot, times a candle), Exchange (CH11)}.
  - When anyone perceives the beat it writes `Foreshadow {Plan, Actor, Beat, Room, Clock, Observers[], Item, LineKey}`.
  - Beats are **not** filed as cards. After the case the notebook's recall list (기억) can surface them, and at most one can be a key clue when it is a path root.
  - Lab target: the player's normal route witnesses at least one beat in ≥50% of cases.
- **Emotional core.**
  - Motive tags: fear, protection, love, desperation, wish/contract, grudge, jealousy, rule pressure (a push announcement from Yusti).
  - `CaseFile.Reframe` holds a LineKey for what the two meant to each other, authored per relationship pattern by the case-drama author from `CharacterBible.md`.
- **Dismemberment has a reason** (`DismemberMotive`), and the key clue follows from it:

  | Motive | Condition | Key clue |
  |---|---|---|
  | Transport | body mass > carrier strength, or a watched route | saw marks + pieces sized for the cart |
  | Capacity | incinerator mouth or trunk size | pieces fit the box |
  | CauseHide | the wounded part is removed | a part missing |
  | Time | parts cooled apart | — |
  | PlaceConfusion | parts spread across rooms | too little blood here |
  | Misdirection | a "madman" story | — |

  Persona affinity only breaks ties. Identity concealment is weak, because Yusti's roll call always knows who is missing.
- **Archetypes (16) and rotation.**
  - LockedRoom, TimeAlibi, MistakenIdentity, VictimBackfire, UnwittingHelper, DoubleEvent, MistakenVictim, VanishingWeapon, VanishingBody, StagedAccident, StagedSuicideOrNatural, RitualStaging, FramedSuspect, ProtectorCoverUp, PanicCoverUp, SealedGathering.
  - The same archetype never runs twice in a row; each loop repeats at most one.
  - Ch1 draws from the simpler half: LockedRoom, TimeAlibi, StagedAccident, VanishingWeapon, PanicCoverUp, FramedSuspect.

### C5 CaseFile — the truth vs presented-story record the trial consumes
```csharp
public sealed class CaseFile {             // Mur.Cases; opened as 'intended' at PlanFormed, finalised on execution and HouseConfirm
  string Incident, Plan, Act, Archetype; int Tier; string AhaRun; SeamRef Hinge;
  StoryLayer[] Layers;                     // [0] first impression, [1] mid reversal, [2] truth
  List<TrickRun> Falsifications; CaseTruth Truth;   // culprit, victim, intended victim, mechanism, agent, CauseSeq/Clock/Room,
                                                    // death clock/room, found room, disposals, true access, roles
  List<Foreshadow> Foreshadows; List<KeyClue> KeyClues; List<ExposingPath> Paths;
  string Reframe, DismemberMotive, Signature;
}
public sealed class StoryLayer { int N; List<Claim> Claims; string BrokenBy; List<string> BreakClues; string SuspectKey; string LineKey; }
public sealed class Claim { Axis Axis; Prop Presented; Prop Truth; List<SeamRef> Seams; }
```
- `CaseApi` exposes Of, Layer(n), Truth, KeyClues, Findings, FairPlay and Timeline (plain-time events for replay/reveal).
- The trial redesign reads **only** `CaseApi`: no ledger scans, no `Impression*` re-derivation, no single-code `ExecutedTrick`.
- Until then the old trial keeps working through adapters: `TrialTricks/TrialMethods.Impression*` return the L1 claims, `ExecutedTrick` maps the aha run to its legacy code, and Logic reads the ClueTag effects.

### C6 Three original example cases (what the system must be able to produce)
1. **"The candle at the cleat"** — TimeAlibi + FramedSuspect, D2.
   - **Setup.** 윤해린 (engineer; she once shipped a defective job) fears 남가온 will publish it. At dusk she helps Yusti lower the chandeliers and asks how the cleat holds (foreshadow). At breakfast she borrows thread from 신채령. At 21:00 she ties the winch loop to the cleat with thread run past a sconce candle cut to burn through at about 22:10, then forges a note in 백이현's hand inviting 가온 to the reading table below. At 22:10 the chandelier falls while 해린 sits in the tea room with four others.
   - **Layers.** L1: an old rope gave way. L2: the rope was released at the cleat, and the note says 이현, who has no alibi. L3: a candle timer set at 21:00.
   - **Hinge.** Wax on a cleat where no candle belongs. Also: the burnt thread end, the stub length (lit at 21:00, when 이현 was swimming in front of witnesses), and the handwriting against the Archive samples.
2. **"His own wine"** — VictimBackfire + UnwittingHelper, D3.
   - **Setup.** 백이현 schemes to drug 권태겸 in the wine cellar and take the ledger, borrowing 강준서's decanter at lunch. 태겸 notices the powder and swaps the glasses while 이현 fetches a candle. 이현 falls asleep. 태겸 wedges the door with a workshop wedge he borrowed at breakfast "for a wobbly table". Cask air kills 이현 overnight, and 태겸 pulls the wedge at 06:00.
   - **Layers.** L1: a drunk man slept in bad air. L2: there was sedative in his glass, so the decanter's owner 준서 is suspected. L3: the sedative was 이현's own (the empty packet in his room), and someone shut him in.
   - **Hinge.** A fresh knee-high splinter on the frame. Also: the missing wedge (loan ledger) and a candle that goes out on the stairs.
3. **"The borrowed gloves"** — MistakenVictim + ProtectorCoverUp, D3.
   - **Setup.** 차도윤 plants a needle in 서라온's gloves, but 라온 lends them to 정세나, cold after swimming (the loan is seen at dinner), and 세나 dies. 차은결 finds her first. To protect her brother she moves the body to the pool and stages a cramp drowning.
   - **Layers.** L1: a swimming accident. L2: drowned by someone, and the gloves point at 라온. L3: a poisoned glove, the wrong victim, and a sister's staging.
   - **Hinge.** A fingertip puncture that matches the glove lining. Also: no froth, livor on the back of a body found face-down, and a drip trail from the music room.

---

## D. Clue generation (implementer 3; delivered through the clues-and-qol API)

### D1 One examination query
- `CaseApi.Findings(sim, examiner, ThingRef)` returns `Finding {ClueTag, LineKey, Props, Visibility, RootKey}`. It merges four sources:
  - physics state (`PhysApi`: post-mortem data, item states, furniture/door extensions, water, fire);
  - traces (TraceKinds → ClueTags);
  - records (breaker log, `LockLog`, `DeviceLog`, `ClockOffset`, loans, washer/incinerator/press logs);
  - CaseFile seams.
- `Evidences.Examine*` (owned by clues-and-qol) calls it with one hook line per function. The four-layer note chains (Tricks → SetPieces → Methods) become adapters and are deleted in Phase 4.
- **ClueTags** is a single table (`Sim/Murder/Clues/ClueTags*.cs`). Each entry has:
  - a const id;
  - a Korean label and a one-line plain meaning (LineBank);
  - the axis it exposes;
  - its `LogicEffect` (Support/Contradict/Limit per claim kind);
  - its channel and visibility;
  - three gore-tier variants.
- `Logic.CheckRaw/Staged`, CaseBoard labels, `TrialGames` fits and SimTests `KeyProps` all read this table. The copied lists are removed, and so is display text used as logic (H8).

### D2 Detectability
- Visibility levels: 0 obvious · 1 near look · 2 detailed exam (20–60 s) · 3 power only.
- What can be found also depends on examiner Obs, room light and freshness. Ageing (wet → dry, warm → cold) changes the text, not whether the thing exists.
- Anything a path needs must have visibility ≤2 or be re-findable by Experiment.

### D3 Exposing paths — the fair-play guarantee
- **Build the paths.** For each falsification, enumerate candidate paths from its seams to their **roots**: a trace, an item state, a body finding, a record, a witness sighting, an experiment.
- **Independence** means disjoint roots with different sources. Copies and hearsay of one source count as one root (P0164). At least one path must use only B/S/O/R/X (not dependent on a single NPC or a power).
- **Admission (`FairPlay.Predict`).**
  - Start from the declared seam templates plus rule guarantees, then subtract the planned concealment. Rule guarantees include: fire leaves metal, bone and a warm furnace; water leaves residue for Afterglow and a filter catch; the cooling curve and livor cannot be erased; a moved object leaves its dust outline.
  - Reject any combination where concealment can erase both paths (P0583).
  - Enforce P0466: the first adjudicated target is not killed in a second incident.
- **Verification after execution.** Check that each path's roots actually exist and can be found. An unplanned loss (someone cleaned, the room burned) is logged as a lab metric and **never repaired by inventing evidence**.

### D4 The key clue set (4–7 cards)
- **What goes in:**
  - the body card;
  - the aha's hinge + one independent confirmation;
  - one seam per supporting trick;
  - one culprit link (possession, prints, witness, record);
  - ≤2 witness sheets;
  - optionally one foreshadow recall when it is a path root.

  Hard cap: 7.
- **API.** `CaseApi.KeyClues(sim, incident)` → `KeyClue {Key ("body:"/"trace:"/"item:"/"furn:"/"door:"/"dev:"/"talk:"), Label, Meaning (one plain sentence), Breaks (claim), Found}`. clues-and-qol's `CaseProgress.Clues` uses it whenever it is non-empty (a hook they own; our adapter until then). Trial pickers show only key clues, at most 6.
- **Richness lives in the trick; the cards stay few.** Everything else can still be examined, is never required, and is never a collection lock.

### D5 Readability, gore tiers, recall
- Times are plain ("밤 10시 조금 넘어"). Jargon is hidden: a lint list covers terms such as "출처", "사후 경직" and "시반". Each thing gets one card, and a witness sheet grows over time.
- Gore tiers 완화/기본/강함 are switchable mid-play. The ClueTag ids, props and paths stay identical across tiers (REQ59); only the decal and wording change. Severed parts, spatter, burns and frost all follow this.
- Foreshadow recalls read like memories ("아침에 채령 씨한테 실을 빌려 가던 해린 씨"), not like evidence jargon.

---

## E. Murder lab (implementer 5)

### E1 Modes
New files `Tests/BL23/SimTests/MurderLab*.cs`, `Continuation.cs`, `PhysicsTests.cs`, `Investigator.cs`, with one dispatch line in `Extra.cs`. Parallelism is by **process**, never threads (static catalogs). Run from a copied bin to avoid DLL locks.

| Mode | What it does |
|---|---|
| `lab gen <N> [seed0] [days]` | child campaigns until N incidents; per-case JSONL + summary |
| `lab natural <seeds> <days>` | unforced distributions |
| `lab forced [family\|id\|all]` | each registry entry forced through `sim.Lab = new LabBias{Force=id}` (instance, not static). Knowledge limits are kept; a documented `TeachPreconditions` fixture may add facts, and the case is then marked *possible-only* |
| `lab cases <N> <out.md>` | a readable report of diverse cases for the critics |
| `lab cont <seeds>` | fork at ≥3 points (daily, mid-plan, investigation) → save/load → step 6,000 ticks → compare canonical JSON |
| `lab perf` | tick mean/p99/max, alloc per tick, save KB and ms |
| `lab physics` | rule tests: cooling, rigor/livor, drying, melt, candle, burn residue, the fall/crush tables, shock conditions (power + fault + contact), gas, dose timelines, WhatIf; Phys save roundtrip; no new Flags keys |

`mystery forced` stays, regenerated from the registries.

### E2 Metrics and targets (a hard miss fails the run)
| Metric | Target |
|---|---|
| Cases with ≥1 executed trick | ≥95% |
| Tricks per case | mean 1.7–2.3, max 3 |
| Boring rate (no trick, culprit seen attacking or caught in the act, or solved by one card) | <3% |
| Share of any one mechanism over 300 cases | ≤12% |
| Share of any one approach / room / archetype | ≤20% / ≤12% / ≤15% |
| Distinct structure signatures (archetype + mechanism group + aha + axes + room) in 200 cases | ≥60 |
| Same archetype twice in a row within a campaign | 0 |
| Every registry entry fires in forced runs | 100% |
| Entries firing naturally within 40 seeds × 8 days | ≥80% (report the rest) |
| ≥2 independent paths per falsification | 100% at admission, ≥98% after execution |
| Key clues per case | 4–7, mean ≈5 |
| Plan abort rate | <25% |
| Wrong-victim rate | 2–8% |
| Knowledge-limited investigator verdict accuracy | ≥70% (today: 17/34 with an omniscient sweep) |
| Foreshadow beat seen on the player's route | ≥50% of cases |
| Determinism | campaign roundtrip IDENTICAL ×5 seeds + `lab cont` IDENTICAL |
| Kernel cost | physics +≤15 µs mean, p99 ≤0.5 ms, 0 alloc; composer ≤2 ms per tick |
| Save growth | ≤100 KB/day |

### E3 Investigator
A knowledge-limited stand-in for the player:
- it examines the body and the found room;
- it follows the trails it can see;
- it reads the published house rules;
- it interviews witnesses;
- it runs one Experiment per case;
- it never sweeps every room.

The headless trial uses it until the trial redesign replaces that.

### E4 Case report (per case, for the critics)
Each case report lists:
- archetype, tier and signature;
- culprit, victim, relationship, motive and reframe line;
- the act (approach / subdue / mechanism / agent / disposals);
- tricks as axis: presented → truth;
- the L1/L2/L3 lines;
- the hinge;
- key clues with their meaning;
- paths with their channels;
- foreshadow beats and who saw them;
- a timeline in plain times;
- failures and improvisations.

### E5 `Game/Core/AutoProbe.Murder.cs`
- **Arguments:** `-murderCase <entryId|archetype> -murderSeed <n>` (run with `build_probe_retry2.sh <name> full BL23_found <seed|rand>`).
- **Behaviour:** sets `LabBias` through Session and time-lapses to the prep and the kill with a ghost camera.
- **Shots:** setup step, the act, ragdoll death, drag/cart, prop reaction, splash/fire/steam/spark, mechanism, aftermath, discovery.
- **Logs:** physics counters (active rigidbodies, ragdolls, `Physics.Processing`), frame ms, exceptions.
- **Mono check:** a save roundtrip plus a 600-tick continuation check.

---

## F. Phased plan

**Phase 0 — hygiene** (the first hours of Implement; each owner does their own):

| # | Fix | Owner |
|---|---|---|
| 1 | Drown exclusion → tag compatibility | #3 |
| 2 | No Message/Swap on bloodless mechanisms | #3 |
| 3 | Replace the dead `CauseEvent.StartsWith("trap")` check with `CauseKind` | #3 |
| 4 | One wrong-victim classification for Topple/Tripwire/Shock | #2 |
| 5 | Cause events for Dose/Plant/Arm → `Incident.CauseSeq` | #2 |
| 6 | Shock needs power + fault + wet contact; Drown needs water | #1/#2 |
| 7 | Topple invalidates nav | #1 |
| 8 | `CutUp` → `Gore.Dismember` with Wound events | #2 |
| 9 | Unity write-backs caused by nobody removed; floor from world height | #4/#1 |
| 10 | Continuation fixes (A7) | #1 + time-on-demand |
| 11 | Replay "Dose" caption names the real agent | #3 |
| 12 | Ordinal/Invariant fixes in owned files | all |

Also in Phase 0, the enum appends: `Stream` (Physics, Murder, Trick), `GameEventType.Phys`, `DamageType` (Cold, Gas, Toxin), and new `Anim` values (Pour, Tie, Saw, Stoke, Wedge, Pull, Ring, Wind, Dig, Wrap, Lift).

**Phase 1 — skeleton** (5 implementers in parallel, disjoint files, §G). Each builds registries + validation, the rule engine, the composer, trick operators, CaseFile/FairPlay/KeyClues, the mirror framework with ragdoll + 6 FX families, and the lab. **All existing content is migrated as registry entries**, with adapters calling old code where the new rule is not yet there. Exit criteria:
- GameCompile "오류 0개";
- campaign on 5 seeds + random: faults=0, IDENTICAL;
- activities fail=0;
- the forced suite regenerated from the registries passes all 19 legacy kinds;
- `lab cont` IDENTICAL;
- `lab gen 200` prints the full metric table (targets reported, not yet enforced);
- AutoProbe.Murder produces one ragdoll case.

**Phase 2 — content** (8 authors, family files only). The catalogue reaches its targets (≥30 mechanisms, ≥60 agents with ≥6 poisons, ≥25 tricks, ≥12 stagings, ≥10 disposals, signatures for every special room, ≥10 archetypes, styles for 17 NPCs). Each entry is forced once. Integration tunes weights until the E2 targets hold over ≥300 cases.

**Phase 3 — detail.**
- Fixtures and FX per room.
- A motion for every step (new `Anim` values mapped in `ActorView`).
- Ragdoll polish.
- Replays with Phys events and history-correct traces.
- Room props via `BL23Lab/foundation/ROOM_PROPS_NEEDED.md` (environment agent).
- Frame budget verified on 2 map seeds.

**Phase 4 — migration and deletion** (only after playtest-and-korean finishes; carry the latest Korean strings into LineBank keys). Delete:
- `Grammar` parsing;
- the static `SetPieces.Force`/`Rules.ForceRule` in murder paths;
- the physical Flags families;
- `Impression*` re-derivation and the `ExecutedTrick` priority chain;
- the Examine note chains and the copied tag lists;
- the trap chains split across Tricks/Methods;
- the retired X_ kinds;
- one-shot `TodShift` writes.

**Phase 5 — trial hand-off.** Document `CaseApi` with the three example cases. The trial redesign (a restart of `trial-fun`) consumes the layers as a fast loop — name the false fact, show its seam, confirm it by an independent path or a re-enactment — with Yusti running procedure and verdict. It is never pedantic and uses none of DR's mechanics.

---

## G. Work split — five implementers, disjoint files

### G1 Ownership
| # | Role | Owns (new) | Owns (existing, migrates) | Delimited edits in shared files (`// ---- murder-foundation begin/end`) |
|---|---|---|---|---|
| 1 | Kernel physical model | `Sim/Physics/`: PhysWorld, PhysTypes (PhysAction, PhysResult, Hazard, Support, enums), Phys (Apply/Tick/BuildLoop/OnLoad), PhysApi, Lethality, Profiles*, ActorPhys, Rules/{Gravity, Thermal, Fire, Water, Electric, Air, Tox, Mech, Contact, Body, Sound, Time}, Fixtures, DoorExt, TraceKinds*, PhysLedger, WhatIf, PlayerPhys (partial Simulation) | `PlayerPhysics.cs` | GameState (+`Phys`) · Simulation (Tick guard, BuildLoop, FromState fixes) · Core (`Stream` append) · Enums (`GameEventType.Phys`, `DamageType`) · WorldTypes (`Register` hooks) · Movement (door queues → timers) · Perception (`ExtraLight`) · Combat (`IsBloodless`, with corpse-discovery) |
| 2 | Act composer + planner migration | `Sim/Murder/`: ActModel (ActRecord, MurderWorld part), Steps, Composer, Planner, Improvise, OpportunityIndex, MurderApi, LabBias, `Catalog/Catalog.cs` (Reg/Build/Validate) + the Approaches/Subdues/Mechanisms/Agents/Disposals/Stagings/Rooms/Schemes/Styles family files (migrated entries) | Crime, Methods, MethodsActs, MethodsDismember | GameState (+`Mur`) · State (`MurderPlan.Act`, `Incident.CauseSeq/CauseKind/IntendedVictim`) · Cases.OnDeath (cause link) · Testimony (IsLethalStep) · Movement.StepMotion (ask `Steps`) · Enums (`Anim` append) |
| 3 | Trick grammar + truth record + clue hooks | `Sim/Murder/Tricks/`: TrickModel, TrickApi, CaseFile (+MurderWorld part), Layers, FairPlay, KeyClues, Findings, Foreshadow, Drama (archetypes, reframes); `Sim/Murder/Clues/ClueTags*`; `Catalog/Tricks_*`, `Archetypes_*` (migrated entries) | Tricks, SetPieces, MethodsClues, Replay, TrialTricks, TrialMethods, Logic (adapters) | Cases.HouseConfirm (`CaseApi.OnConfirm`) · TrialSystem (TrickOf/Reconstruction adapters) · CaseBoard (labels from ClueTags) |
| 4 | Game physics presentation | `Game/Physics/`: PhysMirror, PhysicsLayers, Ragdoll, RagdollBudget, BodyCollider, CarryDragView, PropReact, ToppleAnim, FixtureViews, WaterFx, FireFx, SmokeSteamFx, SparkFx, FrostFx, WetSheen, ItemStateFx, ImpactAudio, ReplayPhys | — | WorldPresenter (2 lines: OnEvent `Phys` case, `Sync` call) · ActorView (ragdoll hand-off, `PlayAnim` mapping of Swim/Drag/Crawl/Stagger/Fall/Hurt/Shove + new Anims) · ItemView (states, no ambient write-back) · PhysicsGrab (provenance, floor) |
| 5 | Murder lab + probe + docs | `Tests/BL23/SimTests/`: MurderLab*, Continuation, PhysicsTests, Investigator; `Game/Core/AutoProbe.Murder.cs`; `Documentation/BL23/MurderLab.md` | Mystery.cs (forced list from registries) | Extra.cs (dispatch) · this bible · DecisionLog · HANDOFF §3 |

**Rules for working in parallel:**
- Each implementer defines only the types listed as theirs (G2). A missing dependency gets a **differently named** temporary shim in your own file (e.g. `ShimCaseApi`), and the integrator removes it; never define another owner's type.
- `MurderWorld` is a `partial` class: #2 declares the `Acts`/`Foreshadow` part, and #3 the `Cases`/`Runs` part.
- Re-read shared files before every edit; never revert others' work.

### G2 Interfaces
```csharp
// #1 → all
public static class Phys     { PhysResult Apply(Simulation, in PhysAction); void Tick(Simulation); void BuildLoop(Simulation); void OnLoad(Simulation); }
public static class PhysApi  { PhysProfile Profile(string type); SupportRec SupportOf(GameState, string item); float AmbientC(GameState, int room);
                               float TempC(GameState, string entity); PostMortem PostMortemOf(GameState, string victim);
                               double ApparentDeathClock(GameState, string victim); bool InWater(GameState, P3, out int body, out float depth);
                               bool Powered(GameState, int furniture); float Gas(GameState, int room, GasKind g); IReadOnlyList<Fixture> Fixtures(GameState, int room);
                               RestPose RestPoseOf(GameState, string actor); FurniturePose PoseOf(GameState, int furniture);
                               IReadOnlyList<string> ContentsOf(GameState, int furniture);   // for time-on-demand Containers.cs
                               float BodyMassKg(string actor); float Strength(string actor); bool IsBloodless(DamageType); float ExtraLight(GameState, int room);
                               WhatIfResult WhatIf(GameState, WhatIfQuery q); }
public static class Lethality { bool Resolve(Simulation, Hazard h); }            // calls MurderApi.Admits
partial class Simulation      { public PhysResult PlayerPhys(PhysAction a); }   // the only Unity → kernel door (validated, provenance)

// #2 → #1, #3, #5, time-on-demand
public static class Steps     { void Register(StepDef); StepDef Get(string kind); Activity Think(ThinkCtx); bool Exec(Simulation, Actor, ActionStep); Anim MotionOf(string kind); }
public static class Catalog   { IReadOnlyList<ApproachDef> Approaches; …Subdues, Mechanisms, Agents, Disposals, Stagings, Rooms, Schemes, Styles; List<string> Validate(); }
public static class MurderApi { bool IsHotStage(MurderPlan); StageKind StageOf(PlanStep); bool IsLethalStep(PlanStep);
                                bool Admits(GameState, Hazard, out string plan); ActRecord Act(GameState, string plan);
                                void OnCause(Simulation, string plan, long seq, string kind); }
public sealed class LabBias   { public string Force; public bool TeachPreconditions; }   // Simulation.Lab (instance field in #2's partial)

// #3 → #2 (composer), #5, clues-and-qol, the future trial
public static class TrickApi  { IEnumerable<TrickOption> Options(ComposeCtx); List<PlanStep> Steps(TrickRun, ComposeCtx);
                                void OnStepDone(Simulation, TrickRun, PlanStep, PhysResult); }
public static class FairPlay  { PathPrediction Predict(ComposeCtx, ActCandidate); FairPlayReport Verify(GameState, string incident); }
public static class CaseApi   { CaseFile Of(GameState, string incident); StoryLayer Layer(GameState, string incident, int n); CaseTruth Truth(GameState, string incident);
                                List<KeyClue> KeyClues(Simulation, string incident); List<Finding> Findings(Simulation, Actor examiner, ThingRef thing);
                                void OnConfirm(Simulation, string incident); IReadOnlyList<TimelineEntry> Timeline(GameState, string incident); }
public static class ClueTags  { ClueTag Get(string id); IReadOnlyList<ClueTag> All; }

// #4 consumes GameEventType.Phys + PhysApi queries; writes to the kernel only via Session → sim.PlayerPhys(...)
// #5 consumes Catalog.*, TrickDef registry, CaseApi, FairPlay.Verify, LabBias, Phys cost counters
```

### G3 Content-author file map (Phase 2; each author touches only these)
| Author | Files |
|---|---|
| weapons | `Sim/Murder/Catalog/Agents_*.cs`, `Sim/Physics/Profiles_Items_*.cs`; one delimited block in `Game/World/ItemView.cs` (MurderProps meshes) |
| methods | `Catalog/Approaches_*.cs`, `Subdues_*.cs`, `Mechanisms_*.cs`, plus the step kinds they need in `Catalog/Steps_Methods_*.cs` |
| tricks-time | `Catalog/Tricks_Time_*.cs`, `Tricks_Sequence_*.cs` |
| tricks-access | `Catalog/Tricks_Access_*.cs`, `Tricks_Place_*.cs`, `Tricks_Identity_*.cs` |
| tricks-cause | `Catalog/Tricks_Cause_*.cs`, `Tricks_Means_*.cs`, `Stagings_*.cs`, `Disposals_*.cs` |
| rooms | `Catalog/Rooms_*.cs`, `Sim/Physics/Fixtures_*.cs`; `BL23Lab/foundation/ROOM_PROPS_NEEDED.md` for the environment agent |
| case-drama | `Catalog/Archetypes_*.cs`, `Schemes_*.cs`, `Sim/Murder/Tricks/Reframes_*.cs`, LineBank `Lines_Murder_*.cs` |
| planner | `Catalog/Styles_*.cs`, the weight tables in `Sim/Murder/PlannerWeights.cs` |

### G4 Requests to other workflows and agents
| Party | Request |
|---|---|
| **time-on-demand** | FromState fixes (`_lastMinute`, `Knowledge.Open`); use `MurderApi.IsHotStage` instead of stage names; `Containers.cs` reads `PhysApi.ContentsOf/SupportOf` (the proximity heuristic stays only as a fallback); keep `OnDemand` in GameState or limit it to scheduling (physics never branches on it); skips run real `Step()` calls |
| **clues-and-qol** | `CaseProgress.Clues` → `CaseApi.KeyClues` when non-empty; `Evidences.Examine*` → `CaseApi.Findings`; witness sheets unchanged |
| **corpse-discovery** | `D.Dismember.Scatter` uses `Gore.Dismember`; `RagdollBudget.FreezeAll()` before `DiscoveryFilm`; the rest pose stays within 0.3 m of kernel `Pos`; `Gore.Bloodless` agrees with `PhysApi.IsBloodless` |
| **characters** | an `ActorAnimator` external-pose suspend/override hook; the `SetBodyColliders` layer; a bone mass map |
| **environment** | chandeliers placed at kernel fixture anchors (`PhysApi.Fixtures`); the props in ROOM_PROPS_NEEDED.md; `OpenableParts` contents mapped to kernel containers; move `StokeFire` to FireFx |
| **cinematics** | `ReplayStage` plays Phys ledger events; reveal beats frame the hinge |
| **playtest-and-korean** | new text lives only in LineBank keys; old-file deletions wait until they finish |

### G5 Decisions to record in `DecisionLog.md`
1. Physics is causal but lethal only through admitted mechanisms (H5).
2. Gore is allowed in 3 tiers with identical clues.
3. Body cooling, rigor and livor are a coarse published house rule that outputs plain windows.
4. Staged accident/suicide/natural deaths are a sanctioned cause axis; the truth is always a person.
5. Unwitting helpers and after-the-fact protectors are ON; a deliberate pre-crime accomplice judged as co-culprit is OFF until the owner decides.
6. 1–3 tricks per case, tiered by chapter; exactly one aha.
7. The player has no lethal verbs; re-enactment experiments are allowed.
8. House rules are published before use, and windows do not open.
9. Terminology: 6 bundles, 4 result types, 11 trick classes.
10. Force hooks are instance-level lab bias and never grant knowledge silently.
