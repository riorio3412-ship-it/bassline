
> **MOTION TRACK (2026-09-28 00:00, character-motion agent): state and how to continue.**
> - **Merge done.** Lab and main Game/Characters are one again:
>   - the split ActorAnimator partials (Combat, IK, Locomotion, Upper, plus the new Reactions, Actions, Paired, Restraint, Ambient) carry main's GPT hooks;
>   - `PhysicsDriven`, `CanApplyPhysicalActions`, `ArmAvailable` and PhysicalActionController.PreparePose / ApplyIK are in;
>   - `Suspended` is an alias of `PhysicsDriven`.
>   Game/Physicality in main is untouched.
> - **The lab has a read-only mirror of the owner's physicality files** in `Game/Physicality` (PhysicalActionController, PhysicalActionStyle, ProceduralContactIK, PhysicalRagdoll), plus `LabPhysicalityStubs.cs` for PhysicalCharacter, PhysicalBodyPart, Session and BL23.Sim.BodyRegion. Refresh it with `bash BL23Lab/sync_physicality_to_lab.sh` whenever main's Physicality changes. install_chars.sh never copies it.
> - **API and names:** ViolenceMotionContract.md, "Published names", lists every name and explains how the pieces fit. New helpers:
>   - `MotionKit.cs` (curves, IK);
>   - `ActorPosesExt.cs` (new gesture and action poses);
>   - companions `ActorLateIK` (order 55, IK between two bodies) and `ActorPostRagdoll` (order 310, the protective reach during a PhysicalRagdoll fall), both added automatically;
>   - `RestraintVisuals` / `RestraintPart` (rope loops, gag band).
> - **Face:**
>   - `ActorFace` has its own clock, `PushTransient(expr, i, secs)`, `SetStrain(gasp, flush, pale)` and `Expr.Choke` / `Panic`. The shader gained `_FaceStrain` (flush / pale inside the face).
>   - Double features fixed at runtime on GLB heads: lid and mouth cells ignore the skin mask (so a closed lid covers the scanned eye), and scanned heads use one mouth path. Talking and gasping move the real jaw with the painted mouth neutral; a painted mouth keeps the jaw shut.
> - **QA:** `CharacterQA.Motion2.cs`, sheets `mo_react`, `mo_everyday`, `mo_attack`, `mo_paired`, `mo_ambient`. Each strip is also saved alone as `Shots/mo/<sheet>_<id>_<LABEL>.png` (full resolution, one row), with `[MO]` debug lines in the log.
>   - Run: `bash charlab_run.sh <owner> BL23.EditorTools.Characters.CharacterQA.ShotsQuick <log> -qaIds P04 -qaSheets mo_react,mo_everyday,mo_attack,mo_paired,mo_ambient` (about 6 min).
>   - `CharacterQA.BakeRetargetShots` bakes (-bakeIds), retargets (-animSets) and renders in one session.
> - **P06 / P10 capes:**
>   - Blender versions without the cape are at `BL23Lab/glb_fixed/P06|P10/*_fixed.glb`, made by `blender_scripts/cape_fix.py`, with before/after images in `glb_fixed/_look/`. They are **not in the bake yet**: the first bake (23:30) showed that
>     - the T-pose arm grab pulled P06's side hair locks into the arms ("hair wings"), and
>     - P10's formerly covered shoulders stayed grey-brown and looked bulky.
>   - Fixes are in the lab (GlbArms: no hair above and medial of the shoulder joint; FixP10: repaint the uncovered shoulders green) but not yet verified.
>   - To try again, copy both GLBs into `Art/Characters/Source/`, point `GlbRigger.Impl.cs` ById at them, then run `BakeRetargetShots -bakeIds P06,P10 -animSets P06,P10 -qaIds P06,P10 -qaSheets lineup,dialogue`.
> - **Still weak:**
>   - The procedural poses are hand-authored, not mocap. Attack wind-ups are readable but stiff, and the stab anticipation is small.
>   - The armpit drag pose needs another pass (the dragger's legs look splayed).
>   - Eye gaze is not animated (the painted eyes have no iris offset); look-at is head / neck / chest.
>   - The kernel still decides where actors stand for paired kills; the animator aligns the victim visually.
>   - P04's fringe strands across the eyes still need a geometry fix (the bake side).

> **USER SCREENSHOTS (main session, 19:25):** the owner asked whether modelling is simply beyond us, because the faces are still "자글거리고 이상한" and the proportions wrong. Fix these first; they are what the owner looks at.
> - **강준서 P10:** the green coat's giant shoulder cape doubles his shoulder width and makes his head look tiny. **Decision:** cut the cape off (Blender) and give him a plain fitted overcoat with normal shoulders. Keep it green, and keep the mole under his left eye.
> - **권태겸 P06:** the mantle does the same. Cut or shrink it the same way. No glasses.
> - **Trial portraits of 차도윤 P04 and 권태겸 P06:**
>   - jagged hair shards cross the eyes;
>   - pink smear marks sit on the skin (face atlas and inpaint artefacts);
>   - a lone red lip blob on P04;
>   - a noisy scarf texture.
>   Repaint the face textures cleanly: smooth skin, clean eyes, a subtle mouth. Rebuild the front hair into clean cards; no outlines on hair crossing the face.
> - The main session raised the portrait render to 1.5x resolution with 4x MSAA. Still check the portraits in the trial studio lighting.
>
> **RAGDOLL API NEEDED BY OTHERS (main session, 18:50).** The user wants real body physics: "시신이 고정된 채 죽는 것 같은데 캐릭터들의 물리를 더 신경써야", plus DRAGGING bodies (weak residents can't carry). Build the rig-level ragdoll in the lab with a stable public API that others adapt to:
> - `ActorRig.BeginRagdoll(Vector3 impulse, float blendIn = 0.15f)`, `EndRagdoll(float getUpTime)`, `bool IsRagdoll`;
> - `ActorRig.PinLimb(HumanLimb limb, Transform target)` for carry and drag grips: drag by the armpits or ankles with the other limbs trailing physically;
> - `ActorRig.SettleOn(Collider)`.
> The murder-foundation workflow (Game/Physics adapter) and the cinematics agent (replay collapse) will call it.
>
> **EXACT CHARACTER API CONTRACT (charpolish design pass, 19:35).** It is in the lab now as stubs (step 0b, lab `Game/Characters`) and reaches main with the charpolish install; code against these exact names:
> - Enums (`CharacterTypes.cs`, append-only): `enum HumanLimb { Hips, Chest, Head, ArmpitL, ArmpitR, HandL, HandR, ThighL, ThighR, AnkleL, AnkleR }`; `enum WeaponClass { None, Knife, Blade, Club, Heavy, Long, Cord, Pillow, Vial }`; `ActionAnim` gains `SwingSide, SwingHeavy, Garrote, Smother, PourPoison, Defend, GrabWrist, Guard`; `Gesture` gains `CallOut, CoverEyes`.
> - `ActorRig`: `BeginRagdoll(Vector3 impulse, float blendIn = 0.15f)`; `EndRagdoll(float getUpTime)` (≤ 0: the body stays exactly as it settled; > 0: a living actor gets up, a dead one blends into the kernel dead pose set with `Anim.SetDeadPose`); `bool IsRagdoll`; `PinLimb(HumanLimb limb, Transform target)` (null releases); `SettleOn(Collider surface)`.
> - `ActorPhysics` (via `rig.BodyPhysics`): `BeginFall(Vector3 fromWorld, Vector3 toWorld)`; `bool RootLocked` (do not write the root position while true); `bool RagdollSettled`; `React(BodyRegion, Vector3 worldDir, float strength)`; statics `RagdollLayer` (10), `RagdollCollideMask`, `MaxActiveRagdolls` (3), `ExternalFallDriver` (set it true when your adapter drives deaths, collapses, falls and drags; the character-side ActorView fallbacks then stay out), `MassFraction(HBone)`.
> - `ActorAnimator`: `PlayAttack(ActionAnim kind, WeaponClass weapon, Vector3? targetWorld = null, float strength = 1f)` returns the seconds until impact; `TimeToImpact`; `SetHeldWeapon(WeaponClass, float massKg)`; `SetCombatReady(bool, WeaponClass)`; `PlayStagger(Vector3 worldDir, float strength)`; `HandsBusy`.
> - The kernel always wins: a settled ragdoll whose facing (up/down) disagrees with the kernel pose blends into the kernel pose within 0.4 s, and the body ends within 0.3 m of the kernel position.
>
> **PLAYTEST TRIAGE FOR CHARACTERS (main session, 18:05):** `C:/Users/리오/BL23Lab/playtest_1626/TRIAGE.md` section (B), with frames in `playtest_1626/frames/`:
> - B2: no hit reaction when struck — add flinch, stagger, turn and fall.
> - B3: identical hand-on-chin trial poses, arms sinking into hips, blocky shoulders, collar spikes.
> - B4: screams with a calm face; rigid twin-tails.
> - B5: P18 invisible at her podium / broken portrait.
> - B6: P10 face mask bulge, white far side of the head, jagged chin shadow.
> - B7: every portrait uses the same face template; opaque glasses on P07 read as a censor bar; Yusti's aquarium head is a flat teal box at a distance.
> - B8: seat snapping.
> - B1 (first-person body blob) is handled in main `ActorView.FirstPersonBody()`: the body draws shadows only when looking down more than 20°, or more than 45° during hand actions.
>
> **BANNED EFFECT (main session, 17:55):** the BREAK hologram glitch in the Toon shader and `ActorAnimator.SetBreak` (magenta/white flicker, scanlines, fresnel glow, vertex jitter) looked like a rendering bug to the user ("빨갰다가 하얬다가 하는 것들 … 전부 없애줘"). Main `ActorView` now always calls `SetBreak(0)`; cornered or cruel trial moments use `ActorRig.SetDarkFace` instead (panic → CorneredStare, counter → HollowGrin). Never re-enable BREAK visuals on any character, including Yusti's aquarium. The shader code may stay, but the amount must be 0.
>
> **NEW TOOL AVAILABLE (main session, 17:35) — the user approved Blender.**
> - The official Blender 5.2.2 LTS portable is installed at `C:/Users/리오/BL23Lab/tools/blender/blender-5.2.2-windows-x64/blender.exe`. It was checked against the official SHA-256 from download.blender.org.
> - Run it headless only: `blender.exe --background --factory-startup --python <script.py> -- <args>`. glTF import/export works; verified with `bpy.app.version_string` = 5.2.2 LTS.
> - Use it for real geometry and rig work that is hard to do in Unity editor code:
>   - reshaping P04's front hair cards;
>   - re-modelling or re-skinning the P06/P10 capes to the A-pose (no wings);
>   - fixing P10's proportions with bone scaling and applied deformation;
>   - cleaning the painted eyes/mouth out of the base textures (texture paint/bake);
>   - adding spring-bone chains for hair, capes and skirts;
>   - segmenting the Tripo thumbs;
>   - better decimation that respects UV seams;
>   - authoring or retargeting animation clips.
> - Keep the source GLBs untouched. Write fixed GLBs to a new folder, e.g. `C:/Users/리오/BL23Lab/glb_fixed/<Pxx>/`, then feed them to the existing CharLab bake. Keep the scripts in `C:/Users/리오/BL23Lab/blender_scripts/` so every fix is reproducible.
> - Blender runs are independent of the CharLab Unity lock, but don't run more than 2 at once (RAM).

Author: the character-art agent (the owner of Game/Characters, Editor/Characters, Art/Characters, Resources/Actors and Shaders/Toon*).
The "character-polish" workflow takes over from here. In this document, **file:line** refers to the lab copy
`C:/Users/리오/BL23Lab/CharLab/Assets/BASSLINE/BL23/...`. Main (`C:/Users/리오/OneDrive/Desktop/BASSLINE/Assets/BASSLINE/BL23/...`)
holds an identical copy, installed at 17:31.

---

## 1. Where things are

| What | Where |
|---|---|
| Lab Unity project (character work only, never the main project) | `C:/Users/리오/BL23Lab/CharLab` |
| Runtime code (ActorRig / Animator / Face / Skeleton / Poses / Clips) | `Assets/BASSLINE/BL23/Game/Characters/` |
| Bake tools (GLB pipeline, procedural builder, QA, retarget) | `Assets/BASSLINE/BL23/Editor/Characters/` |
| Baked assets (meshes, textures, face atlases, materials) | `Assets/BASSLINE/BL23/Art/Characters/Baked/<ID>/` |
| Source GLBs from the user | `Assets/BASSLINE/BL23/Art/Characters/Source/`: `GLB_minhyuk/jinwoo/doyun` (P01/P02/P04, via `LookSpec.Model`), plus `P05_CrimsonGentleman.glb`, `P06_Distributor.glb` and `P10_Junseo.glb` (assigned by id, `GlbRigger.Impl.cs:29`). Originals: `C:/Users/리오/BL23Lab/glb/<name>/` |
| Prefabs (loaded by `ActorFactory` via Resources) | `Assets/BASSLINE/BL23/Resources/Actors/<ID>.prefab` |
| Retargeted clip sets | `Assets/BASSLINE/BL23/Resources/Actors/Anim/{PROC,P01,P02,P04,P05,P06,P10}.bytes` |
| Animation sources (CC0, Quaternius UAL1/UAL2) | downloads in `C:/Users/리오/BL23Lab/dl/anim` (see `SOURCES.md`); FBX copies in the lab only: `CharLab/Assets/LabOnly/UAL/` (never installed) |
| Credits | `Art/Characters/CREDITS_CHAR.md`, copied to main `Art/ThirdParty/CREDITS_CHAR.md` |
| Toon shaders | `Assets/BASSLINE/BL23/Shaders/ToonCharacter.shader`, `ToonCommon.hlsl`, `ToonForward.hlsl`, `ToonPasses.hlsl` |
| QA renders | `C:/Users/리오/BL23Lab/CharLab/Shots/*.png` |
| Bake / QA logs | `C:/Users/리오/BL23Lab/CharLab/Logs/<logname>.log` and `Logs/bake_report.txt` |
| Lab backups of the sources before the 52-bone change | `C:/Users/리오/BL23Lab/labbak_1510/`. Main's folders before the 13:48 install: `C:/Users/리오/BL23Lab/backup_chars_1348/` |

## 2. Running the lab (bakes and QA)

**Only one Unity process may run on CharLab at a time.** The lab has no lock file, so coordinate inside the workflow and never
start a second one. Kill only a PID you started yourself: check the command line first (`-projectPath C:/Users/리오/BL23Lab/CharLab`).
Never run Unity on the main project; main builds go through the main session's `locked_build_probe.sh`.

Runner: `C:/Users/리오/BL23Lab/run.sh <Class.Method> <logname> [args...]`. It runs Unity in batch mode on CharLab, then prints
`exit N` followed by any `error CS` or exceptions found in the log.

| Task | Command (run from `C:/Users/리오/BL23Lab`) | Time |
|---|---|---|
| Bake some actors | `bash run.sh BL23.EditorTools.Characters.CharacterBaker.BakeSome bake_x -bakeIds P04,P06` | ~1 min startup, then 10–30 s per GLB actor (decimation is cached) and 15–70 s per procedural actor |
| Bake everything (19 actors) | `bash run.sh BL23.EditorTools.Characters.CharacterBaker.BakeAll bakeall_x` | ~9 min |
| QA sheets | `bash run.sh BL23.EditorTools.Characters.CharacterQA.ShotsQuick qa_x -qaIds P04,P06 -qaSheets dialogue,lineup` | 1–3 min |
| Retarget clip sets, then QA | `bash run.sh BL23.EditorTools.Characters.CharacterQA.RetargetThenShots qa_x -animSets P05,P06,P10 -qaIds ... -qaSheets ...` (`CharacterQA.cs:48`) | +2 min |
| GLB face calibration photos | `-qaSheets glbface -ids P05,P06` (or `GlbFace.DebugMasks`, `GlbFace.cs:272`), written to `Shots/glbface_<ID>.png` and `glbface_preview_<ID>.png` | 1 min |

Useful QA sheets (the dispatch is in `ShotsQuick`, `CharacterQA.cs:51`):
- `lineup`: `quick_front`, `quick_34` and `quick_back`.
- `dialogue` (`CharacterQA.cs:657`): warm key light, dark room, 9 expressions. Add `-noOutline` or `-cullBack` for diagnostics.
- `darkface` (`:698`).
- `eyezoom` (`:733`): eye close-ups on the full-resolution source, raw vs fixed, texture-only vs lit.
- `scan` (`:592`): raw vs texture-fixed scan turnaround.
- `fp` (`:863`): first person, player P01 only.
- `motion` (`:901`).
- `hands`, `closeup`, `talk`, `expr`, `faces`, `portrait`.

A pre-compile check that needs no Unity (lab Game/Characters against main's other Game code):
`dotnet build -c Release` in `C:/Users/리오/AppData/Local/Temp/claude/C--Users----OneDrive-Desktop-BASSLINE/2927cf85-ff74-4e21-abc8-f9c9d50f8a0c/scratchpad/precompile`.

## 3. Installing into main

- `bash C:/Users/리오/BL23Lab/do_install_chars.sh` does the whole sequence:
  1. Takes the shared lock `C:/Users/리오/BL23Lab/main_unity.lock` with `mkdir`, retrying every 60 s.
  2. Runs `install_chars.sh`.
  3. Copies `CREDITS_CHAR.md` to `Art/ThirdParty/`.
  4. Releases the lock with `rmdir`.
  5. Runs GameCompile.
- `install_chars.sh` mirrors four folders (`Game/Characters`, `Editor/Characters`, `Art/Characters`, `Resources/Actors`) and the four Toon* shader files.
  - It copies folder **contents** (`cp -rf dir/.`). `rm -rf` of a busy folder aborts under `set -e`.
  - It then deletes files that exist in main but not in the lab.
  - **Check main for foreign edits in these folders before installing**: the mirror overwrites them.
- GameCompile: `cd C:/Users/리오/OneDrive/Desktop/BASSLINE/Tests/BL23/GameCompile && dotnet build -c Release`. The tool prints Korean text: 오류 = errors, 빌드했습니다 = build succeeded. Errors in `Game/Cinema` or `Game/Mansion` usually belong to agents who are mid-edit; re-run.
- Keep the public runtime API that other modules use:
  - `ActorRig.SetHeadHidden`, `HeadHidden` and `EyeWorld` (`ActorRig.cs:207`, `:215`).
  - `HBone.Head`, `HandAnchorR`/`HandAnchorL`, `EyeAnchor`.
  - `SetExpression`, `PlayAnim`/`MapPose` (ActorView), the IdleLife gestures, the `Body` collider child, and the judge-seat Sit.
  - `Rig.Anim.FirstPersonCalm = Rig.HeadHidden;` is wired in main's `Game/World/ActorView.cs:55`.

## 4. GLB actors (P01, P02, P04, P05, P06, P10): bake pipeline

The entry point is `GlbRigger.BakeImpl` (`GlbRigger.Impl.cs:112`). In order:
1. **Load and normalise.** `LoadScan` (`:59`) converts glTF to Unity axes, scales to `CastDef.HeightCm`, puts the feet at y=0, centres on the pelvis, detects landmarks (`GlbAnalysis`) and applies the per-model eye-centre correction (`GlbFaceCalib.Apply`, `GlbFace.cs`).
2. **Texture fixes** (`:123`, `GlbFix.Texture`, `GlbFix.cs:18`). The base map is resized to 2048. `GlbTexPaint.Build` (`GlbTexPaint.cs:26`) maps every texel to its bind-pose position and normal, then the per-model fixes in `GlbFix.Models.cs` run:
   - P06 (`:235`, away from the Ouma look):
     - checker scarf to solid camel, using a checker detector (`:59`) grown to whole UV islands (`:115`, `:140`) plus a knot zone;
     - chest V to a cream shirt and burgundy tie;
     - grey iris to amber (`IrisMask`, `:216`); hair and brows to chestnut, with the eyeliner kept dark;
     - red buttons to brass, white jacket to navy, blacks to a brown mantle, charcoal trousers and brown shoes.
     - The user rejected glasses; `Glasses()` in `GlbFix.Geometry.cs:72` is kept but not called.
   - P10 (`:335`): skin patch in the fringe repainted as hair, grey coat to bottle green, hair noise smoothed, mole under the left eye (`Mole`, `:359`).
   - P05 (`:372`): left-glove smear to black, red specks in the hair removed, mouth-corner smear inpainted with a clean gold tooth.
   - P04 (`:420`):
     - `CleanPaintedStrands` (`:438`) wipes the eye surround to skin and turns dark strokes across the eye whites into sclera;
     - `EyeBoost` (`:511`) deepens the iris and adds a pupil and catch light;
     - the turtleneck is toned to off-white.
   - Helpers:
     - `Retint`, which keeps luminance structure (`GlbTexPaint.cs:75`);
     - `Dot` (`:100`) and `Inpaint` (`:118`);
     - `PadEdits`, which fills the UV gutters (`:164`).
3. **Accessories** (`GlbFix.AddAccessories`, `GlbFix.Geometry.cs:17`): currently none.
4. **Decimation.**
   - The budget is `GlbTriBudget = 48000` (`GlbRigger.Impl.cs:48`). UV-seam vertices stay locked, so Tripo meshes stop at about 88k (P06, P10) and Meshy meshes reach 60–88k.
   - Before decimating, the bake removes floating fragments (`GlbCleanup.cs:15`) and cuts P04's fused fringe in front of the eyes at full resolution (`CutFringe`, `GlbFix.Geometry.cs:267`, which fits a face plane per eye).
   - Results are cached in `Library/BL23GlbCache`, keyed by source file + budget + `c1` + (`f<GeometryVersion>` when a fringe cut applies). **Bump `GlbFix.GeometryVersion` (`GlbFix.Geometry.cs:14`) whenever pre-decimation geometry edits change.**
   - `MeshDecimator` needs `EdgeKeyComparer` (`MeshDecimator.cs:37`). `long.GetHashCode` made the edge maps quadratic, and P14 and the Tripo meshes took over 10 minutes. Always pass the comparer to `Dictionary<long, ...>` edge maps.
5. **Post-decimation geometry** (`LiftFringe`, `GlbFix.Geometry.cs:159`):
   - The "lift" (squashing strands upward) is **disabled for everyone** (`FringeLift` values are 0 at `:150`) because it stretched skin-coloured strand undersides into combs.
   - "Tuck behind the eyes" (`PushBehind`, `:152`) is on for P06 only; it hides the big strand behind his eye.
6. **Arms.** `GlbArms.Classify` (`:161`) computes arm membership (flood + capsule + T-pose lateral grab), then weights; the arm chain comes from centreline slices. `SolveTargets`/`SolveTwist`/`Apply` (`:201`) repose the arms with a dual-quaternion blend into the rest pose. Prefabs set `KeepArmRestBend` and `ArmIdleOut`.
7. **Hands.** `GlbHands.Segment` (`GlbHands.cs:30`) segments the digits with a merge tree over the hand; `ApplyWeights` (`:216`) gives 3 bones per digit; `ToProportions` (`:251`). Tripo hands (P06, P10) find 4 fingers and no thumb, so the thumb uses the generic `PlaceHand`.
8. **Face** (`GlbFace.cs`, `GlbFaceMask.cs`, `FacePainter.Glb.cs`):
   - Per-model calibration: `GlbFace.cs:33` (EyeCX/EyeCY/EyeDX, mouth, chin, eye-opening shape in mm).
   - Face frame: `MakeFrame` (`:49`), a 21 cm square in face uv (`uv1.xy`); `uv1.z` is the front gate.
   - Skin mask:
     - `GlbFaceMask.Rasterize`/`Classify` (`GlbFaceMask.cs:38`, `:134`) photographs the textured head from the front and classifies each pixel as skin, strand or prior.
     - The result is saved as `_FaceMask`; the shader multiplies every overlay by it.
   - Overlay atlas: `PaintGlbAtlas` (`FacePainter.Glb.cs:37`).
     - Eye cells are lids painted in the scan's own lid colours; `LidK` (`:60`) shrinks partial lids on narrow eyes.
     - Mouth cells are skin covers plus procedural mouths; `OpenMouth`/`Gritted`/`MouthDepth`/`LipHints` are at `FacePainter.cs:569-640`.
   - FX atlas: `PaintGlbFx` (`:222`) holds the base nose and lip shading (always on for GLB), blush, gloom and tears.
   - Blendshapes: `AddBlendShapes` (`GlbFace.cs:159`) builds SmileL/R, JawOpen, BrowUp/Down and CheekUp, limited by the feathered skin mask. `ActorFace` drives them.
9. **Mesh attributes:**
   - uv0 = texture uv;
   - uv1 = face uv plus gate plus **outline reduction in `w`**; `ThinOutlinesNearEyes` (`GlbFix.cs:89`) sets w = 1 on everything in the eye band;
   - uv2 = bind position;
   - **uv3 = per-vertex masks (x = hair, y = skin)** from `VertexMasks` (`GlbFix.cs:60`);
   - tangent = outline normal. The smooth normal is used unless it disagrees with the vertex normal (thin cards), then the vertex normal (`GlbRigger.Impl.cs:284-297`).
10. **Material:** a single material per GLB with `VertexMasks = true`, `HairRing = 0.05`, `SkinSSS = 0.3` (`GlbRigger.Impl.cs:232`). **LODs:** `AddLods` (`CharacterBaker.Lod.cs:69`) keeps 0.45 and 0.18; seams are unlocked at LOD1/LOD2; the LODGroup switches at 0.28, 0.1 and 0.006.

Calibration workflow for a new GLB:
1. Run `-qaSheets glbface -ids <ID>` and look at `Shots/glbface_<ID>.png` (768 px image covering 0.21 m, so 1 display px ≈ 0.42 mm at the 500 px preview scale).
2. Set EyeCX/EyeCY/EyeDX/EyeDY/Mouth*/ChinDY and the eye-opening shape (EyeOut/EyeIn/EyeTop/EyeBot in mm) in `GlbFace.cs:33`.
3. **P05, P06 and P10 still use the default eye-opening shape**; see §9.

## 5. Procedural actors (P03, P07–P09, P11–P18, NPC00)

- `ProcBuilder` (partial: `ProcBuilder.cs`, `ProcGarments.cs`, `ProcAccessories.cs`, `ProcHair.cs`, `ProcHairClumps.cs`, `ProcHands.cs`) builds SDF bodies and garments and meshes them with `SurfaceNets`/`CharMesher`.
- Hair is made of clump shells (`ProcHairClumps`, `UseClumps = true`).
- Hands: `ProcHands.PlaceFingerBones` (`ProcHands.cs:25`) and `HandRule` (`:58`) put the 52-bone fingers on the modelled finger polylines.
- The face atlas comes from `FacePainter.PaintAtlas`.
- Unapplied work: phase 2 is prepared but **not** applied. The script is `apply_phase2.sh` in the scratchpad above; it covers:
  - scan-derived eye templates (`GlbEyeTemplate.cs`, `FacePainter.Scan.cs`);
  - head shape (`buildhead.txt`), face layout (`setupface.txt`) and base shading (`paintbase.txt`);
  - sleeve and leg folds (`ProcFolds.cs`);
  - the brocade fix.
- Applying phase 2 means BakeAll and a lineup/faces QA. The coordinator reports that the procedural cast looks crude next to the GLB six; this is the biggest open quality gap.

## 6. Skeleton, rig and physics API

- `HBone` has 52 bones (`ActorSkeleton.cs:11`, `Count = 52` at `:25`):
  - 21 core bones;
  - `UpperChest`;
  - thumb plus index/middle/ring/little × 3 segments per hand, left then right.
- `ActorSkeleton.Digit(left, d, seg)` (`:83`), `PlaceHand` (`:171`), and `Parent`/`Order`/`Mirror`/`Core`.
- **Remapping old prefabs:** `ActorRig.Init` (`ActorRig.cs:61-80`) re-finds extended bones by name when a prefab has 21 or 34 bones. Old two-segment finger bones, such as `FingersL1`, simply drop out.
- Old clip files (other bone count) keep only the core bones plus UpperChest (`ActorClips.cs:67`).
- `FingerTipRest[10]`, and `FingerTipAnchors[10]` created at bake time (`CharacterBaker.cs:335`); `FingerTipAnchor(left, digit)` (`ActorRig.cs:275`).
- `SetBodyColliders(bool on, int layer = -1)` (`ActorRig.cs:288`): kinematic capsules and boxes on head, neck, chest, abdomen, pelvis, arms, hands, legs and feet; off by default. Put them on a layer the actor's own controller ignores.

## 7. Animation

- **`ActorAnimator`:**
  - Procedural pose layers plus a **clip layer**. The clip set is chosen at `ActorAnimator.cs:265`: `AnimSet`, else the actor id for GLB actors, else `PROC`.
  - Walk/jog/sprint clips are matched to ground speed, plus sit/stand transitions, death collapse, action clips and gesture clips.
  - Canonical pose application: `ApplyPose` (`:879`), parent-first; finger curl comes from the 6 hand channels. Each digit is spread-corrected toward the hand axis; `_spreadFix` is set up at `:430`.
  - `FirstPersonCalm` (`:751`, `ApplyFirstPersonCalm` `:754`): arms low (swing up to about 8°), calmer torso, low carried items. QA sheet `fp_P01.png`.
  - `FingerToLipsIK` (`:796`): two-bone arm IK after the pose is written, so the index tip lands on the lips.
- **Clip sets:** `ClipRetarget.RetargetAll` (`ClipRetarget.cs:82`) retargets about 51 UAL clips through a Humanoid avatar built from each rig straightened into a T-pose. It samples with a PlayableGraph, reads the result back to canonical space and writes quantised `.bytes` (magic `BLAC`). Per-set rest poses matter: re-run `-animSets <ID>` after any change to a GLB actor's arm repose.
- `ActorPose`/`ActorPoses`: `GesturePose` (including the new `Gesture.FingerToLips`), actions (Throw/Garden/Craft/Examine/Search/Swim appended to `ActionAnim`), hand shapes.

## 8. Dark faces, FX and shader

- **API:** `ActorRig.SetDarkFace(DarkFace kind, float intensity = 1f, float hold = -1f)` (`ActorRig.cs:138`); state via `DarkFaceState`.
  - Kinds: `DarkFace { None, HollowGrin, CorneredStare, VeiledSmirk }` (`CharacterTypes.cs:11`).
  - `ActorFace` (`ActorFace.cs:157-205`): the shadow creeps in over 0.35 s, the glow fades in after it, and both revert over about 0.45 s. `DarkShape` drives the blendshapes; `SnapDarkFace()` is for QA.
  - VeiledSmirk also plays `Gesture.FingerToLips`.
  - Glow colour per actor: `ActorFace.DarkGlow`.
- **Shader:** `BL_DarkFace` (`ToonForward.hlsl:157`) draws procedurally in face uv from `_FaceEye`/`_FaceMouth`, masked by `_FaceMask`; the composite is at `:416`. Properties `_DarkFace` and `_DarkFaceColor`.
- **Toon shader changes this session** (`ToonForward.hlsl`):
  - key-light clamp (`:332`);
  - painted-shadow saturation and the skin branch: softer ramp, warm shade, `skinK` (`:338`);
  - weak terminator warmth;
  - angel-ring hair highlight gated by the uv3 hair mask (`:385`);
  - highlight shoulder (`:397`);
  - albedo-tinted rim (`:406`).
- In `ToonPasses.hlsl`: albedo-tinted outline colour (`:65`); outline width × (1 − uv1.w) (`:44`).
- New properties: `_ShadeSat`, `_SkinSSS`, `_SSSColor`, `_MaskMode`, `_OutlineTint`, `_DarkFace`, `_DarkFaceColor`, all in the CBUFFER. `MakeMat` sets them (`CharacterBaker.cs:400+`).

## 9. Known problems (priority order)

1. **Double features: blink over open eyes, moving mouth plus painted mouth.**
   - Eyes, cause A: on GLB faces the lid overlay is multiplied by `_FaceMask`, the skin mask (`ToonForward.hlsl:76`, in `BL_ComposeFace`). Pixels of the scanned eye, lashes or strands classified as non-skin cannot be covered, so the painted-open scan eye shows through a "closed" lid. Fix: force the mask to 1 inside the calibrated eye opening plus about 3 mm (and the mouth rect) in `GlbFaceMask.Classify`. Alternatively, let lid cells ignore the mask inside the opening polygon.
   - Eyes, cause B: **P05, P06 and P10 have no measured eye-opening shape**. EyeOut/EyeIn/EyeTop/EyeBot are still the defaults in `GlbFace.cs:41-43`. The lids are the wrong size and position, so the scan eye peeks around them. Measure them from `glbface_<ID>.png` as was done for P01/P02/P04.
   - Mouth: on scanned heads `ActorFace` drives **both** the JawOpen blendshape while talking (`ActorFace.cs`, `TickShapes`, about `:242-255`) **and** the painted TalkA/TalkO/TalkClosed cells (`Push`, `:263-285`). Pick one per actor type. Suggested: on `_hasShapes` heads keep the painted mouth neutral while talking and use only the jaw and smile shapes, or disable the jaw shape and keep the painted mouths. Then check the `talk` and `dialogue` sheets.
2. **P10's proportions look wrong**, according to the user.
   - The Tripo model is in T-pose with a shoulder cape. The arm repose drags the cape into horizontal "wings" at the shoulders, which reads as huge shoulders.
   - The head also looks small for a 187 cm body, and the hands are large with only 4 segmented fingers.
   - Check the cape weights in `GlbArms.Classify`, since cape vertices get arm membership in T-pose. Options: skin the cape to UpperChest/Shoulder with smoothing, or run a cloth-drape pass after the repose.
   - Also compare `LoadScan`'s landmarks (`Shots/splat_arms_P10.png`, `splat_repose_P10.png`).
3. **P06/P10 capes:** same cause as item 2 (wings at the shoulders in the lineup).
4. **P04 fringe:** strands are cut and wiped around the eyes and outlines removed in the eye band, which is much better. A dark lock still hangs over his left eye (image-right) in close-ups. A real fix means replacing the front fringe with procedural hair cards, or remodelling. Diagnostics:
   - `eyezoom_P04.png`;
   - `Shots/splat_cut_P04.png` (red = cut, yellow = in front of the face plane);
   - the `-noOutline` dialogue variant.
5. **Procedural cast quality** is well below the GLB six. Phase 2 (§5) is prepared; hair, face atlas and silhouettes need the most work.
6. The Tripo hands (P06, P10) have no segmented thumb, and LOD0 on the Tripo meshes is about 88k tris because UV seams are locked; a seam-aware (wedge) collapse would allow 48k.
7. Experiments that failed and were rolled back (don't repeat them blindly):
   - the vertical fringe "lift", which stretched strand undersides;
   - a skin-on-hair repaint driven by rear-most skin depth, which blackened the nose and eye sockets;
   - a tuck-behind-eye on P01, P04 and P10, which produced z-fighting specks in 3/4 views.

## 10. What I would do next

1. Fix the double features (§9.1). Measure the eye-opening shapes for P05, P06 and P10, force the face mask inside the openings, and give scanned heads a single mouth path.
2. P10 and P06 capes and proportions: cape skinning or drape, plus head/hand scale sanity against `CastDef` and the other GLBs.
3. Apply phase 2 to the procedural cast, BakeAll, then lineup and faces QA before/after for all 18 plus Yusti.
4. Motion and physics: use `SetBodyColliders` for grabs and props; add spring chains for capes, coats and hair on GLB actors (`SpringChain` exists for procedural hair); re-run `RetargetAll` for all sets after any rest-pose change.
5. P04: procedural front fringe cards.
