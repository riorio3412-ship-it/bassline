# Violence, reactions and body-action contract (2026-09-27 22:30)

This is the shared contract between the **physicality/violence track** (main: Sim + Game/Physicality + Game/World + new Game/Violence) and the **character motion track** (lab CharLab: Game/Characters, installed into main).

## The owner's request (verbatim, the /goal of this session)

> "…일단 밀면 자연스럽게 밀리고 때리면 자연스럽게 아파하며 넘어질 때 탁자나 뭔가에 손을 뻗는다거나 놀란다거나 행동하고 칼을 휘두르는 모션이나 물건을 건네는 모션 탁자를 짚거나 물건을 옮기거나 줍는 모션 흉기에 따른 캐릭터 훼손도나 가구들이 파괴되거나 움직이는 등. 이것들 외에도 캐릭터들의 다양한 반응들이 중요해. 뭐 죽이는 모션 목이 졸리는 모션 그에 따른 시간 예시로 머리채를 잡고 익사시킨다고 하면 자연스럽게 몇십초간의 바둥거림이나 고통스러워하는 표정 물이 튀김 이런 것들이 있겠지? 이것 외에도 필요한 모션이나 물리들은 네가 구현해내. 따로 결박할 수도 있겠고. 총기류 무기도 있으면 좋을 것 같아. 석궁도 좋고 총기도 좋고. 이것 외에도 트리플 A게임이라면 있는 캐릭터들의 행동들 이런 것들이 필요해."

## Foundation (keep; extend, never rewrite)

The owner built a physicality layer in GPT on 2026-09-27, 21:43–22:00. It compiles and is partly wired.
- **Sim side:**
  - `Sim/Physicality/PhysicalBody.cs`: reactions (Startle / Flinch / Guard / Stagger / Brace / Fall / Downed / Incapacitated), postures, per-region Bruise/Cut/Puncture/Structural, and Pain/Balance/Consciousness/Mobility/Grip.
  - `Sim/Physicality/BodySolver.cs`: deterministic impact → reaction arbitration and recovery.
- **Game side:**
  - `Game/Physicality/PhysicalCharacter.cs`: contact/balance motor, Shove/Bump/OnWound/Startle/Collapse, and a support raycast for bracing.
  - `PhysicalActionController.cs`: Reach / PickUp / Place / Give / Receive / Brace / Swing with contact callbacks and IK.
  - `PhysicalRagdoll.cs`: a lazy joint-limited proxy that is active only while down or recovering.
  - `PhysicalMelee.cs`: wind-up, swept contact and recovery.
  - `PhysicalProp.cs`: material damage and load-bearing.
  - `PhysicalGrab.cs`, `PhysicalItemTransfer.cs`, `ProceduralContactIK.cs`.
  - Editor QA scenes in `Editor/Physicality/*`.
- **Wiring:** ActorView creates `PhysicalCharacter`. Interaction uses `PhysicalMelee` and `Shove`. ActorAnimator (main copy) calls `PhysicalActionController.PreparePose` and `ApplyIK`.

**The lab and main copies of Game/Characters have DIVERGED.**
- Main has the GPT hooks in ActorAnimator, ActorFace, ActorRig and ActorSkeleton.
- The lab has half-written motion files from the stopped character-polish workflow: `ActorAnimator.Combat/IK/Locomotion/Upper.cs`, `ActorPhysics.cs`, `CharacterContracts.cs`.
- The character track must merge both. Main's GPT hooks must survive every install.

## Motions and reactions required

Every one of these must look natural, be interruptible where plausible, and be driven by kernel state (determinism).

### Reactions (character track animates; physicality track drives)
- **Push:** a stagger with step recovery, or a fall if balance is lost. It is direction-aware (front, back, side).
- **Hit:** a flinch by region (head snaps, hand to wound, doubled over after a gut blow). Pain expression.
- **Fall:**
  - arms reach out to break the fall;
  - grab a nearby table, chair or rail edge (brace) when one is in reach;
  - otherwise a ragdoll collapse onto real geometry;
  - get up after a delay scaled by pain.
- **Startle:** a small jump or turn toward the sound or source. Hands up when something falls near them.
- **Stumble on stairs.** Slipping on a wet floor.
- **Death:** a physical collapse. Never snap into a corpse pose.

### Everyday body actions (character track animates; PhysicalActionController does the IK)
- Pick up from floor, table or shelf with the proper bend (squat vs stoop by height).
- Place, hand over and receive: two actors synchronised at the moment of contact.
- Carry light, heavy and two-handed objects, with weight in the posture.
- Lean or rest a hand on a table; push a chair; open doors and drawers with the hand on the handle.
- Drag a body by the armpits or the ankles; carry over the shoulder (strength-gated).

### Attacks (character track animates; physicality track resolves contact)
- Knife: stab (underhand and overhand) and slash.
- Blunt: overhead swing, side swing, two-handed heavy swing.
- Shove and throw.
- Pour poison.
- Every attack has wind-up → strike → follow-through → recovery.
- The victim gets defensive reactions: arms up, grabbing the wrist, turning away.

### Prolonged kills (NEW; paired, time-true)

| Method | Duration | What happens | Consequences |
|---|---|---|---|
| Strangle (manual, rear ligature, front ligature) | victim struggle 10–40 s by strength and surprise; unconscious; death after a further hold | hands or cord at the neck; the victim claws at the attacker's arms and neck and kicks, and their legs weaken; attacker braces | the attacker can be scratched or bitten (marks and evidence); items get knocked over |
| Smother (pillow) | same phases, shorter struggle | the pillow is pressed down | a sleeping victim wakes and fights |
| Drown (grab the hair and force the head into water: pool, bath, fountain, sink, well) | several tens of seconds of thrashing, then limp | splashing, bubbles and gurgles; water sloshes onto the floor | wet clothes and floor, hair in the attacker's hand, and bruises on the scalp and neck |

For all three:
- Phases: approach → seize → struggle → weaken → unconscious → dead.
- The attack can be interrupted, e.g. by someone arriving or a sound, and the victim may survive.
- Sounds propagate (thuds, choking, splashes, a scream if released).
- Pain and panic faces (eyes wide, mouth gasping), a face that reddens then pales, and limbs that go limp at the end.

### Restraint (NEW)
- Tie wrists and ankles and gag with rope, cord, tape or a scarf. It needs a subdued, sleeping or sedated victim, or two people.
- A bound actor has restricted movement: hops or crawls, can't use their hands, and can struggle to free themselves over time (rope loosens), leaving marks on the wrists.
- The binding and the rope are evidence.

### Firearms and crossbow (NEW)
Gothic-era choices: a revolver, a hunting shotgun or rifle, a dueling pistol, and a crossbow with bolts.
- **Where they come from:** gun cabinet, trophy room, study; ammunition is separate.
- **Handling:** load, aim, fire, recoil; the crossbow is cocked slowly.
- **Hearing:** a gunshot is heard house-wide (a huge alarm); a crossbow is almost silent (a trick opportunity).
- **Ballistics:** kernel-deterministic hit test along the line of fire, where walls, doors and people occlude. Wounds are through-and-through or embedded; a bolt stays in the body or a wall.
- **Traces:**
  - gun: powder residue on the hand and sleeve, a casing (pistol), a bullet hole in furniture or walls, a muzzle flash seen through a window;
  - crossbow: the bolt, the string.
- **Motions:** aim stance; shoot (recoil); reload; draw from the coat (concealment system).
- **Victim:** hit reaction by region, then a collapse.

### Weapon-specific body damage (visual)
Cut slits, stab holes, bruises and swelling, gunshot entry and exit wounds, burns, strangulation marks, broken nails and scratches. Blood follows the gore palette. `ActorWounds` + `Gore` show these on the body and clothes, driven by `Actor.Physical` regions and the Body wounds.

### Furniture
- `PhysicalProp` handles breakage and movement.
- Struggles knock things over. Falling bodies break chairs and tables when the energy is high enough.
- Bullets and bolts damage furniture.
- The kernel records these as traces (evidence).

### AAA-style ambient behaviour (character track)
- **Idle life:** look-at with eyes, head and chest; weight shifts; small idle fidgets per personality.
- **Crowds:** people make way for each other with shoulder turns; avoid bumping, and say sorry on contact.
- **Nearby events:** flinch at loud noises; cover the mouth at horror; hug or console someone who is crying.
- **Everyday tells:** check a pocket watch; adjust clothing; hands to a burn when touching something hot; shiver in the cold rooms.

## Interface rules
- **New motion names:** new `ActionAnim` and `Gesture` values are appended to the enums in Game/Characters (lab). The character track publishes them in this file under "Published names", and installs a contract build early so main compiles.
- **Until then:** the violence track may call them through `Enum.TryParse` with a fallback, so it is never blocked.
- **Paired motions:** the character track exposes `ActorAnimator.BeginPaired(PairedKind kind, ActorAnimator partner, Transform contactA, Transform contactB, float struggle01)` or an equivalent. The violence track feeds it the struggle intensity each tick from the kernel assault state.
- **Kernel is authoritative** for outcomes and durations. The game presents them, and cosmetic physics never decides who dies.

## Published names (the character track fills this in)

**Contract install 22:48 (in main now; append-only, names never change).** Bodies are filled in batches; until a body lands, the call is safe and falls back to the nearest existing motion. All in `BL23.Game.Characters`.

Merge note: main's GPT hooks live on in the merged `ActorAnimator` (`PhysicsDriven`, `CanApplyPhysicalActions`, `ArmAvailable(left)`, `PhysicalActionController.PreparePose` before and `ApplyIK` after the pose). `Suspended` is now an alias of `PhysicsDriven`. Game/Physicality is untouched.

**Enums (`CharacterTypes.cs`)**
- `ActionAnim` += `GetUp, Startle, Slip, StumbleStairs, HitHead, HitGut, ClutchWound, HandOver, Receive, LeanTable, PushChair, OpenDrawer, CarryHeavy, DragArmpits, DragAnkles, LiftBody, Pour, StabUnder, StabOver, Kick, Aim, Shoot, Reload, DrawWeapon, Holster, CockCrossbow, TurnAway, Dodge, Drown, LigatureFront, TieUp, Untie, StruggleBonds, Hop` (after the step-0b `SwingSide, SwingHeavy, Garrote, Smother, PourPoison, Defend, GrabWrist, Guard`).
- `Gesture` += `CoverMouth, Console, Hug, CheckWatch, AdjustClothes, Shiver, Apologize, DuckCover, HandToBurn, HandsUp, WeightShift`.
- `WeaponClass` += `Pistol` (revolver, dueling pistol), `Rifle` (hunting shotgun / rifle), `Crossbow`.
- `Expr` += `Choke` (gasping), `Panic`.
- New: `PairedKind { None, Strangle, GarroteRear, LigatureFront, Smother, Drown }`, `PairedRole { Attacker, Victim }`, `[Flags] RestraintFlags { None, WristsFront = 1, WristsBack = 2, Ankles = 4, Gag = 8 }`, `DragGrip { Armpits, Ankles, Wrists }`.

**`ActorAnimator` (via `rig.Anim`)**
- Reactions: `PlayStagger(Vector3 worldDir, float strength)`; `PlayHit(BodyRegion region, Vector3 worldDir, float strength)`; `PlayFall(Vector3 worldDir, float strength)` (call just before `PhysicalCharacter` hands the body to `PhysicalRagdoll`; a `PhysicalActionController.Brace` in progress becomes the reach target); `float PlayGetUp(float pain01 = 0)` (returns seconds); `PlayStartle(Vector3 sourceWorld, float strength)`; `HearNoise(Vector3 sourceWorld, float loudness01)`; `SetPain(float pain01)`; `SetWoundHold(BodyRegion? region)`.
- Everyday: `SetDragging(ActorRig body, DragGrip grip)` (null stops), `IsBeingDragged`; `SetCarryLoad(float massKg, bool twoHanded)`; `LeanOn(Vector3? surfacePointWorld, Vector3 surfaceNormalWorld, bool left = false)`; `SetHandTarget(bool left, Transform target, Vector3 localOffset, float weight = 1)`; `PlayAction(ActionAnim, seconds)` for every new ActionAnim. The squat-vs-stoop, handover lean and carry weight around `PhysicalActionController` actions are automatic (no call needed).
- Attacks: `float PlayAttack(ActionAnim kind, WeaponClass weapon, Vector3? targetWorld = null, float strength = 1)` returns seconds to the impact / shot frame (kinds: Stab, StabUnder, StabOver, Slash, Overhead, SwingSide, SwingHeavy, Shove, Kick, Throw, Pour, PourPoison, Shoot); `TimeToImpact`; `SetHeldWeapon(WeaponClass, float massKg)`; `SetCombatReady(bool, WeaponClass)` (with Pistol / Rifle / Crossbow = aim stance); `SetAim(Vector3? targetWorld)`; `float PlayDefense(ActionAnim kind, Vector3 attackerWorld)` (Defend, GrabWrist, TurnAway, Dodge, Guard); `PlayAction(Reload | DrawWeapon | Holster | CockCrossbow, seconds)`.
- Paired kills: `BeginPaired(PairedKind kind, PairedRole role, ActorAnimator partner, float struggle01 = 1)` (call on either actor; the partner gets the other role), `SetPairedIntensity(float struggle01)` each tick (1 = thrashing, 0 = limp), `EndPaired()`; `PairedActive`, `PairedRoleNow`, `PairedPartner`; `static PairedPlacement(PairedKind, out Vector3 victimLocalPos, out float victimLocalYaw)` (victim root relative to the attacker root).
- Restraint: `SetRestraint(RestraintFlags)`, `SetRestraintStruggle(float)`, `Restraint`.
- Ambient: `PassBy(Vector3 otherWorld)`, `Bumped(Vector3 fromWorld, bool apologize = true)`, `SetCold(float cold01)`, `ConsolePartner(ActorRig other, float seconds)`, `SetLookAt`, `Glance(point, seconds)`.

**`ActorRig`**: `SetStrain(float gasp01, float flush01, float pale01)` (face gasping / reddening / pallor; paired kills drive it themselves).

**Additions (motion build, 2026-09-28 00:05; append-only):**
- `ActorAnimator.SetPairedSurface(float metres)`: water surface for Drown (bath 0.55, basin 0.85, fountain 0.5) or bed top for Smother (0 = floor). Call after `BeginPaired`.
- `ActorAnimator.SetStairs(Vector3 bottomWorld, Vector3 topWorld, int steps)` (steps <= 0 = off): feet land on the treads, the hips drop to the lower foot. Call while the actor is on a flight (kernel `StairId >= 0`).
- `ActorAnimator.IsDragging`, `IsAttacking`, `Reacting` (hit / startle / fall / get-up running), `CanonicalRotation(HBone)`.
- `ActorFace.PushTransient(Expr, float intensity, float seconds)`: a wince / surprise over the kernel expression that reverts by itself (the reactions use it).

**How the pieces fit (what the physicality / violence track calls, and what is automatic):**
- **Hit:** `PlayHit(region, attacker->victim direction, severity01)`. Strong hits (>0.55) add a directional stagger; the face winces. PhysicalActionController's own pain IK (hand to the contact point) still runs and agrees with it.
- **Push:** `PlayStagger(pushDir, strength)`. Feet stay planted in the world while stepping; if PhysicalCharacter moves the root, the body does not double the travel. A brace in progress (controller `Brace`) stops the body short of the support.
- **Fall:** call `PlayFall(dir, strength)` when balance goes (e.g. on `PhysicalPosture.Falling`), just before the ragdoll starts. While `PhysicalRagdoll` owns the bones, `ActorPostRagdoll` (order 310, added automatically) bends the arms to break the fall and splays the hands until the body settles. No ragdoll: the fall finishes procedurally in LieFront / LieBack.
- **Get up:** automatic. `PhysicalCharacter` calls `SetPosture(LieBack); Ragdoll.End(); SetPosture(Stand)`, and the animator reads how the body really lies (face up, face down or half sitting) and plays the matching get-up. `PlayGetUp(pain01)` forces one.
- **Everyday:** automatic around PhysicalActionController. PickUp / Place / Reach get a squat for the floor, a hip hinge with the free hand on the thigh for low tables, and tiptoes for high shelves. Give / Receive get a lean and nod, Brace a lean, and a carried mass over 2.5 kg gets weight posture. `SetCarryLoad` is for loads the controller does not know.
- **Drag:** `SetDragging(bodyRig, DragGrip.Armpits | Ankles | Wrists)` on the dragger (null ends it). The body is attached to the dragger like a shoulder carry, laid out with a lolling head and trailing limbs; the dragger's hands are IK'd to the armpits / ankles. Main `ActorView` now maps `Anim.Drag` and uses `ViolencePlans.Dragging` in OnCarry / RestoreFromKernel; `ReplayStage` calls `SetDragging` for recorded drags (ankle grip when the ledger says "ankle"/"feet").
- **Attacks:** `PlayAttack` returns the impact time (heavier weapon = longer wind-up / recovery). `PlayAction(Stab, ...)` from the kernel routes to the same beats. While PhysicalActionController runs its Swing contact window, the drawn arm is the attack beat's (the window and its events are untouched). Firearms: `SetHeldWeapon(Pistol|Rifle|Crossbow, kg)` + `SetCombatReady(true, cls)` + `SetAim(point)` gives the aim stance (pistol = side-on duellist, the arm aimed exactly by IK); `PlayAttack(Shoot, cls)` recoils; `PlayAction(Reload | DrawWeapon | Holster | CockCrossbow, secs)`.
- **Paired kills:** `attacker.BeginPaired(kind, PairedRole.Attacker, victim.Anim, 1)`, then `SetPairedIntensity(s)` every tick. The victim is aligned to `PairedPlacement` visually even if the kernel put it ~15 cm off, and sinks to the knees as s drops below ~0.55. Its face gasps, reddens over ~14 s, then pales after going limp; the attacker's face strains. At 0 the victim is limp and the attacker still holds. `EndPaired()` blends both back to whatever the kernel says (dead pose, unconscious, standing). Water splashes, the pillow prop and sounds are the violence track's.
- **Restraint:** `SetRestraint(WristsFront | WristsBack | Ankles | Gag)` draws rope loops and a cloth gag, poses the arms (the wrists IK together) and turns walking into hops. `SetRestraintStruggle` makes the actor writhe. Bound wrists block PhysicalActionController hand actions (`CanApplyPhysicalActions` = false).
- **Ambient:** weight shifts and personality fidgets (CheckWatch / AdjustClothes for the formal styles) are automatic. Call `PassBy` when two people cross in a corridor, `Bumped` on contact, `HearNoise(src, loudness)` for gunshots (duck and cover) and slams (flinch), `SetCold` in cold rooms, and `ConsolePartner` next to someone crying. Turning on the spot now steps the feet instead of pivoting on them.

## Violence track: what it calls and what it still asks for (2026-09-27 23:45)

**Called (by reflection through `Game/Violence/CharBridge.cs`; each call falls back to an existing motion until its body lands):**
- **Paired kills:** `BeginPaired(kind, Attacker, victim.Anim, s)`, then `SetPairedIntensity(Assault.Intensity)` each frame, and `EndPaired()` on both actors when the kernel assault ends. `SetPairedSurface` gets bath 0.55, sink/basin 0.85, fountain 0.5, well 0.8, shallow water 0.1 and bed 0.55; the pool rim is 0. Kind mapping: StrangleManual → Strangle, StrangleRear → GarroteRear, StrangleFront → LigatureFront, Smother, Drown.
- **Face:** `ActorRig.SetStrain(gasp, flush, pale)`, driven from the phase: struggle flushes, weaken gasps, unconscious/dead pales. The expressions are `Choke` / `Panic` / `Scream` with Pain / Fear fallbacks.
- **Restraint:** `SetRestraint(WristsBack|Ankles|Gag)` from the kernel `Binding`; `SetRestraintStruggle` while the bound actor works at the knots.
- **Drag:** `SetDragging(bodyRig, Armpits)` and `PlayAction(Drag)` whenever `Violence.Dragging(S, carrier)` is true; `SetDragging(null)` when it stops.
- **Firearms:**
  - Aiming: `SetHeldWeapon(Pistol|Rifle|Crossbow, kg)`, `SetCombatReady(true, cls)` and `SetAim(target)`. The stance is released when the NPC's kernel Attack step ends.
  - Firing and reloading: `PlayAttack(Shoot, cls, target)` on each shot; `PlayAction(Reload | CockCrossbow, loadSeconds)`.
- **Reactions:**
  - `PlayDefense(Defend | GrabWrist | TurnAway | Dodge, attacker)` on a kernel Strike.
  - `PlayStagger(away, 0.8)` when a victim wrenches free.
  - `HearNoise(src, loud)` / `PlayStartle` for gunshots within 40 m.
  - `PlayHit` / `PlayFall` through PhysicalCharacter.
- **Actions:** `TieUp`, `Untie`, `Aim`, `Shoot`, `Reload`, `CockCrossbow` and `Drag`. **Gestures:** `Recoil` and `HandsToMouth`.

**Kernel state the Game reads** (`BL23.Sim.ViolenceState`, `S.Violence`):
- `Assault {Kind, Phase, Attacker, Victim, Intensity 0..1, Water, WaterType, At/AttAt, Yaw/AttYaw}`. `Active` is true while `Phase <= Unconscious`.
- `Binding {Actor, Material, Wrists, Ankles, Gag, Loose, Off}`.
- `ShotRecord {WeaponType, From, Y0, Yaw, Pitch, Impacts[Kind, Actor, Region, Furniture, Door, At, Y, Dist, Through]}`. The event `Anim "shot|id"` fires it.
- `Embedded {Item, Actor, Region | Furniture/Door, At, Y}` holds bolts in bodies and walls.

**Still requested from the character track** (nice-to-have; the fallbacks work without them):
1. A seated-victim garrote variant (victim in a chair, attacker behind the chair back). The kernel sets `Assault.Seated`.
2. A kneeling-over-the-rim pose for a pool drowning (surface 0 = the attacker kneels at the rim, the victim's head below floor level).
3. A victim loop of clawing at the attacker's forearms or at the ligature during the Struggle phase, with the hands IK'd to the attacker's wrists.
4. A bolt-hit reaction: `PlayHit` with a short freeze and a look down at the wound, instead of the blunt stagger. Bolts stay parented to the region bone.
5. (Done on this side: the aim target plays `Gesture.HandsUp` + `Panic`.) A proper backing-away walk with the hands up while a gun stays on the actor would be better.
6. A hop-and-fall variant for bound actors on stairs.
