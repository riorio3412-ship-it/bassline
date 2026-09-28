# BL23 performance report — 2026-09-27

Written by the perf workflow for the agents that own each area. The user asked for periodic optimisation without
losing anything they can see ("최적화도 간간히 해줘"). Every recommendation below says what it would cost visually.

**How it was measured.** The machine is always busy, so wall-clock ms are noise (±5–40 ms). The comparisons below use
counters that do not depend on the machine:

- **Unity, per frame:** draw calls, SetPass calls, triangles, shadow casters and vertex-buffer upload bytes (ProfilerRecorder
  in the release player), plus a census of the scene.
- **Kernel:** allocated bytes per tick and the final save's hash (identical output means identical behaviour).

Kernel milliseconds are given only for back-to-back A/B runs in one process.

Sources:

- `probes/perf_before` — the Unity perf probe, seed 777.
- `probes/perf_levers` — the same probe plus A/B "levers" (§3).
- Kernel runs in a scratch harness that compiles the tree's `Sim/` sources with an instrumented `Nav.cs` / `Movement.cs` /
  `Perception.cs`: 3 seeds × 2 days, the same loop as `SimTests tickcost`.

---

## 0. Top offenders, ranked

The deltas come from the kernel-paused A/B levers in §3 (`probes/perf_levers`, seed 777). A lever switches a feature
off completely, so it gives the **upper bound** of what a gentler fix can save; the screenshots show what that costs
visually.

Reference frames, kernel paused:

| scene | draw calls | SetPass | triangles | shadow casters | skinned vertex upload |
|---|---|---|---|---|---|
| hall | 21.0 k | 2,889 | 37.3 M | 3,161 | 40.1 MB/frame |
| dining (breakfast, 16 people) | 8.3 k | 2,216 | 30.7 M | 2,406 | 45.3 MB/frame |
| dialogue close-up | 9.2 k | 2,651 | 33.6 M | 2,616 | 49.4 MB/frame |

| # | offender | measured | cause | expected win | visual risk | owner |
|---|---|---|---|---|---|---|
| 1 | **Planar reflection re-renders the scene every frame** | Off: hall draws −43 %, SetPass −49 %, tris −42 %. Dining −36 / −47 / −33 %. Dialogue −36 / −48 / −32 %. | `MansionAtmosphere.cs:296-345`: `CopyFrom(main)` (the same culling mask), far plane 60 m, every frame, in 24 room types (`:282-291`), including the hall, dining, library and corridors. | Keep it but make it cheap: no clutter layer 27, far plane 20–25 m, `lodBias` ≈0.5 for that camera only. Estimate: half the upper bound, about −15–25 % draws and tris in reflective rooms. | Off entirely loses the faint gloss on the dining parquet (shots 09 vs 10). The cheap version keeps it. | Mansion |
| 2 | **Hall draws every room on the floors above and below** | Emulated (`~hallcull`): hall draws **−56 %** (21.0 k → 9.3 k), shadow casters −53 %, tris −27 %, visible renderers 1,533 → 588. **No visible change** (shots 02 vs 07). | `MansionView.cs:427`: `Math.Abs(rf - f) == 1 && inHall` shows all of floors ±1. The 30/48 m cull (`:429-434`) is own-floor only. There is no portal visibility. | The same order as measured once the hall keeps only hall, landing, stairwell and rooms that open onto the void. | None (all hidden rooms are behind floors and walls) | Mansion |
| 3 | **People cast shadows at full detail** | Off (`~charshadowoff`): dining tris **−29 %**, shadow casters −36 %, draws −11 %. Dialogue −30 % / −39 % / −11 %. Hall −12 % / −18 % (only 1 shadowed light there). | Every person casts LOD0 (60–180 k tris) into up to 6 cube faces × each shadowed lamp. | LOD1 (35–45 k tris) as a ShadowsOnly proxy with LOD0 casting Off: about 60 % of that, so −15–18 % tris in crowd scenes. | None to see (shots 09 vs 11: the difference is hard to find even with shadows fully off) | Characters |
| 4 | **Character LOD switches too far away** | `lodBias` 2 → 1 (every transition at half the distance): hall tris −19 %, skinned upload −35 %. Dining tris **−30 %**, upload −38 %. Dialogue close-up −4 %. | 13 of 19 characters have 2 LODs switching at `screenRelativeHeight 0.2` (`Resources/Actors/P03,P07–P09,P11–P18,NPC00.prefab`) → about 18 m at FOV 72 × `lodBias 2` (`QualitySettings.asset:301`). A 22 m hall is all LOD0. | Move them to the 3-LOD scheme P01/P02/P04/P05/P06/P10 already use (0.28 / 0.1). | None to see at 9–13 m (shots 09 vs 12) | Characters |
| 5 | **CPU skinning** | Vertex-buffer upload **40–49 MB per frame**: exactly 40 B × every visible skinned vertex, and it scales with LOD (lever 4 changed it by the same %). | `ProjectSettings.asset:107-108` `gpuSkinning: 0`, `meshDeformation: 0`. Ultra `skinWeights: 255` (`QualitySettings.asset:286`). | GPU (batched) skinning: upload → ≈0, and skinning ~1 M verts/frame leaves the CPU. | None | Lead (shared file); character-polish verifies faces and gore |
| 6 | **Point-light shadows (6 cube faces each)** | All off (`~shadows0`, upper bound): hall draws −15 %, tris −17 %. Dining −29 % / −34 %. Dialogue −28 % / −36 %. | `MansionView.cs:409-419`: budget 4 within 10 m; up to 56 lights (`:410`). | Budget 2 in rooms with ≥ 6 people, or spot-type shadows for ceiling fixtures: about half the upper bound. | Visible if overdone: all-off flattens the tables and chairs (shots 09 vs 13). Keep the key lamp. | Mansion |
| 7 | **Kernel garbage: almost all managed allocation** | Hall with the house running: **1,385 KB/s**. The same scene with the kernel paused: **73 KB/s**. `Pathfinder.Find` = 52–80 % of kernel time and 70–92 % of kernel allocation (630–790 KB and 0.6–2 ms per call). | `Sim/World/Nav.cs:214-307`: dictionaries + `SortedSet<(float,long)>` per call. Unreachable goals are retried every ~10 ticks (§2.5). | **Patch ready** (`perf/Nav.PathfinderFast.cs`, saves IDENTICAL): kernel B/tick −67…−91 %, Find time −70…−82 %. | None | Nav.cs is unowned → lead; retry behaviour → cast-voice |
| 8 | **Conversation camera far plane 140 m** | 25 m (`~far25`): dialogue draws −14 %, SetPass −10 % | ConversationCamera `farClipPlane` 140 | as measured | None (shots 19 vs 24) | DialogueUI owner (clues-and-qol) |
| 9 | **Mesh memory** | 6,737 meshes, 19.7 M vertices, ≈1.35 GB estimated; total used 1.96–2.17 GB | No `UploadMeshData` anywhere (`MeshBuilder.cs:398-410`, `MansionView.Furnish.cs:70`, `PropPerf.cs:55`). Float4 vertex colours. Character meshes 124–132 B/vertex, all float32. | −0.5–1 GB RAM (UploadMeshData). Compact formats: −30–50 % mesh RAM/VRAM and build. | None / verify the toon data channels | Mansion; Characters |
| 10 | **Presentation garbage and main-thread work** | Kernel-paused lever windows: 16–73 KB/s. The trial (world frozen): 660–1,051 KB/s (estimated, noisy). | `Hud.cs:131-132` and `UpdateInfo()` (`:177+`) rebuild strings, a `List` and LINQ every frame. `ActorRig.cs:246` `.name` per hand child. `ActorRig.cs:164-177` about 800 `Material.SetFloat` calls/frame (blood/wet re-set every frame). `ActorAnimator` animates hidden people. `Trial/Gothic.cs:153` `SetVerticesDirty()` every frame. | Rebuild on change only; early-outs. | None | clues-and-qol, character-polish, trial-reforge |
| 11 | **Build 795 MB** | meshes 274.8 MB, textures 237.9, sounds 149.0 (audio done), fonts 23.7 | Character bodies 10–29 MB (float32 streams). `StripUnusedMeshComponents: 0` (`ProjectSettings.asset:193`) | −100–150 MB (compact character vertex formats) | Low (verify in CharLab) | character-polish, lead |
| 12 | **Kernel leftovers after the patch** | 5.0–6.2 KB/tick | `LifeAI.cs:37+ ChooseLife` 2.2–3.1 KB/tick. `Simulation.cs:169-184` Guard delegates ~0.8 KB/tick. Grammars 0.7 KB/tick | about −50 % of what remains | None | cast-voice, lead |

The levers do not add up: the reflection also draws people and their LOD, and the other-floor rooms include lamps
that cast shadows.

---

## 1. Safe fixes applied in the free files

Every fix keeps behaviour identical:

- **Kernel:** final saves are IDENTICAL, old code vs new code, in one build (seeds 20260926 / 777 / 4242 × 2 days). `campaign 20260926 3` and `campaign 777 3` pass with faults=0 and round-trip IDENTICAL; `activities` gives ok=80 fail=0.
- **Unity:** GameCompile has 0 errors, and the Unity probe ran with them (§3).

| file | change | effect |
|---|---|---|
| `Sim/Systems/Perception.cs` (`See`, `_doorOpen` field) | The sight ray's door test was a lambda capturing `this`, so a new delegate was allocated per ray (about 5 rays/tick). Now one cached delegate reads the door when asked. | Perception allocation **421–485 → 88–98 B/tick (−79 %)** |
| `Sim/Systems/Movement.cs` `Held()` | `new[] { HandR, HandL }` per call → right hand then left, no array. Called for every person each observer sees (≈6 calls/tick). | Part of the −79 % above, and grammars −7 B/tick |
| `Sim/Systems/Movement.cs` `HasKey()` + `Opens()`/`KeySeg()` | `Pocket.Concat(new[]{…})` + `KeyFor.Split(';')` per call → a plain loop over the same segments (ordinal compare, the same master-key rule). Called from the path search's door cost for every believed-locked door. | −3…−77 B/tick. Up to 3.8 MB over 2 days on seed 20260926 (20 k calls). |
| `Game/World/WorldPresenter.cs` `Sync` → `PruneTraces()` | Every frame this made a `_traces.ToList()` copy plus a `S.Traces.FirstOrDefault(closure)` per shown trace, so O(shown × all) and a closure each. Now one pass over each list with reused collections, the same first-match rule and the same destroy order. | 0 B/frame instead of ≈(40 + 50·shown) B/frame. Grows with every murder (traces are never removed within a loop). |
| `Game/World/ActorView.cs` `LegHurt()` | Two `Wounds.Any(lambda)` per person per frame (each boxed the list enumerator) → a loop. | ≈ −1.5 KB/frame with 19 people |
| `Game/UI/SpeechBubbles.cs` `LateUpdate` | `List.Sort(lambda)` every frame (allocates a comparer) → a swap when there are 2 captions (the same order as the sort; there are at most 2). | −1 allocation/frame while captions show |
| `Game/Player/PlayerController.cs` `HasFlashlight()` | `Sim.Carried(...).Any(...)` (iterator + array + LINQ) every frame while the flashlight is on → hands and pocket directly | −≈200 B/frame in dark scenes |
| `Game/World/ItemView.cs` `FixedUpdate` | Up to 4 engine calls (`Visual.transform.position` ×2) per item per physics step → 2 (read once; re-read after a write-back, same semantics) | 217 items × 2 fewer engine calls per physics step (≈5 steps/frame at 10 fps) |
| `Game/Player/PhysicsGrab.cs` `FurnitureSync.FixedUpdate` | Player-touch dictionary lookup only once the piece has rested 0.5 s (was every step) | Small; 183 pieces |
| `Game/Core/AutoProbe.PerfLevers.cs` (new) + 4 call lines in `AutoProbe.Perf.cs` | Probe only: kernel-paused A/B windows for the recommendations (§3) | Measurement |

Not done here, because it is outside the free files or not exactly behaviour-preserving: the Nav patch (§2.4) and the
kernel retry back-off (§2.5).

---

## 2. Recommendations by owner

### 2.1 Mansion (environment agent) — `Game/Mansion/*`, `Environment/Rendering/*`

1. **Visibility from the hall** (`MansionView.cs:427`): `bool vis = rf == f || Math.Abs(rf - f) == 1 && (inHall || IsVertical(rv.Room));`
   makes every room on the floors above and below visible while the camera is in the hall, landing or stairwell. There
   is also no distance limit for other floors (`:429` applies to `rf == f` only).
   - For `rf != f`, keep GrandHall/Landing/Stairwell plus rooms whose rect is within ~6 m of the hall void and have an
     opening onto it.
   - Apply the same 30/48 m limit to them.
   - Better still, a portal pass: BFS from the camera's room through open doors and wall openings (`Layout.Walls()` with
     `Open || DoorId >= 0` and `Doors[d].Open`) to depth 2–3. Rooms not reached are hidden.
   - This also fixes the corridor (6.4 k draws, 17 rooms) and library (9.2 k draws, 18 rooms) windows, where rooms
     behind walls within 30 m are drawn.
   - Measured effect: §3 `~hallcull`.
2. **Planar reflection** (`MansionAtmosphere.cs:296-345`) renders the whole visible scene a second time every frame in
   24 room types (`:282-291`), including the hall, dining, library and corridors. Do any of these; none should be
   noticeable at half resolution with mip blur:
   - Give the reflection camera its own `cullingMask` without the clutter layer (27) and without small decor.
   - Set `farClipPlane` 60 → 20–25 m (`:339`).
   - Set its `layerCullDistances` to about 8 m for layer 27.
   - Draw it at low LOD: set `QualitySettings.lodBias = 0.5` in `RenderPipelineManager.beginCameraRendering` for the
     planar camera and restore it in `endCameraRendering`. People in the reflection are then LOD1/LOD2.
   - Optionally render it every 2nd frame when the camera moved less than 2 cm / 0.5°.
   - Measured upper bound: §3 `~noreflect`.
3. **Shadowed lights** (`MansionView.cs:409-419`):
   - Set `shadowBudget` to 2 when the camera room holds ≥ 6 people (the crowd is what multiplies shadow triangles),
     and keep the nearest key light.
   - Ceiling fixtures whose light mostly goes down can use spot-type shadows (one view instead of 6).
   - Measured upper bound: §3 `~shadows0`. The character share is `~charshadowoff`.
4. **Light updates:** `foreach (var l in AllLights) ApplyLight(l);` (`MansionView.cs:421`, also `:296`) sets
   `intensity` and `enabled` on **all 818 lights** every 0.2 s (1,636 engine calls) although at most 56 are candidates.
   Touch only the lights whose visibility or factor changed; keep the previous state in `LightRec`.
5. **Mesh memory:**
   - Call `mesh.UploadMeshData(true)` on finished static meshes (room shells, merged furniture/decor, fixtures) once
     colliders are baked and any `CombineMeshes`/vertex reads are done. That is `MeshBuilder.ToMesh` callers,
     `MansionView.Furnish.cs:70`, `MansionView.PropPerf.cs:55` and `CourtroomView.WellBuild.cs:1078`.
   - Today every runtime mesh keeps its CPU copy: 19.7 M vertices, ≈1.35 GB estimated.
   - Where vertex colours are plain 0–1 tints, use `SetColors(List<Color32>)` (4 B instead of 16). Check that no
     channel packs HDR or data first.
6. **Rendering asset** (`Environment/Rendering/RP_BASSLINE_URP.asset`, shared):
   - MSAA 4× (`:28`) **and** SMAA High on every camera (`MansionAtmosphere.SetupCamera`). Offer MSAA 2× as a graphics
     option (the GPU is triangle- and fill-bound in crowds). Not a default change without the user seeing it.
   - SSAO (`RD_BASSLINE_Forward.asset:18-20`) runs at full resolution from DepthNormals. `Downsample: 1` is a cheap
     option (slightly softer AO).
7. **Dialogue close-up**: 5.4–9.2 k draws and 1.6–2.6 k shadow casters (`perf_before` / `perf_levers`) for a head-and-shoulders shot. §3 `~far25` measures a
   25 m far plane on the conversation camera: draws −14 %, SetPass −10 %, no visible change (owner of that camera:
   DialogueUI / clues-and-qol).

### 2.2 Characters (character-polish) — `Game/Characters/*`, `Art/Characters/*`, `Resources/Actors/*`, the lab

1. **GPU skinning:** switch `ProjectSettings.asset:107-108` to `gpuSkinning: 1` / `meshDeformation: 2` (GPU batched).
   This is a lead decision because the file is shared. Verify in CharLab that:
   - the face blend shapes work (`ActorFace.TickShapes` → `SetBlendShapeWeight`);
   - `GorePieces.cs:284` / `GoreTorso.cs:136` `BakeMesh` still produce the cut (BakeMesh skins on the CPU itself and
     works with GPU skinning).

   Expected: vertex upload 40–49 MB/frame (§3) → ≈0.
2. **Skin weights:** Ultra `skinWeights: 255` (`QualitySettings.asset:286`) → 4, if the bakes have ≤ 4 influences
   (check in the lab).
3. **LOD:**
   - 13 characters (P03, P07–P09, P11–P18, NPC00) still use 2 LODs switching at `screenRelativeHeight 0.2`, which is
     about 18 m at FOV 72 with `lodBias 2`. So everyone in a 22 m hall is LOD0 (80–180 k tris).
   - Move them to the 3-LOD scheme P01/P02/P04/P05/P06/P10 already have (0.28 / 0.1 → about 13 m / 36 m).
   - Measured with `lodBias 1` (all transitions at half the distance): §3 `~lodbias1`.
4. **Shadow proxy:**
   - LOD0 `SkinnedMeshRenderer.shadowCastingMode = Off`, plus the LOD1 mesh (35–45 k tris) as a `ShadowsOnly` renderer
     on the same bones inside LOD0.
   - Shadows are drawn up to 6 × lights times per person; the upper bound is §3 `~charshadowoff`.
   - With GPU skinning the extra skinned renderer is cheap.
5. **Submeshes:** 13 characters have 15–27 submeshes (materials), so every pass costs 15–27 draw calls per person.
   The single-material bakes (P01, P02, P05, P06, P10) show the atlas route works; the rest can follow.
6. **Vertex format and build size:**
   - Bodies are 124–132 B/vertex, all float32: colour float4, uv1 float4, uv2 float3, and uv3 float2 on P01.
   - Bake with `SetVertexBufferParams`: colour UNorm8×4, uv0 Float16×2, uv1/uv2 Float16, normal/tangent SNorm16 where
     the toon outline data allows.
   - Expected: about −45 % character mesh bytes in RAM, VRAM and the build (character meshes are most of the 275 MB of
     "Meshes").
   - Keep `m_IsReadable: 1` on the bodies: Gore reads them (`GoreMeshCut`, `BakeMesh`).
7. **Per-frame garbage** in `ActorRig.LateUpdate` → `HasForeignChild` (`ActorRig.cs:243-247`): `t.GetChild(i).name`
   allocates a new string for every child of both hand anchors of every person every frame.
   - Cache the built-in `Prop_` children as a `HashSet<Transform>` at build time.
   - Or compare `childCount` with the cached count and test membership.
   - Result: exact, no allocation.
   - `ShowBuiltIn`/`SetPropsVisible` allocate `new[] { HandAnchorL, HandAnchorR }` too, but only on change.
8. `ActorRig.SetVisible` (`ActorRig.cs:197-201`) → `GetComponentsInChildren<Renderer>(true)` (a new array) on every
   visibility change. ActorView hides people whenever their room is culled, so this runs each time the camera changes
   room. Cache the renderer array once (refresh when a disguise or prop is added).
9. **Material writes every frame:** `ActorView.Tick` (`ActorView.cs:149`) calls `Rig.SetBloodied(..)` and
   `Rig.SetWet(..)` every frame. `ActorRig.cs:164-177` then runs `m.SetFloat(..)` on **every material of the rig**,
   changed or not.
   - That is 19 people × 15–40 materials (all LODs) × 2 = about 800 engine calls per frame, and each touched material
     may re-upload its SRP-Batcher constant buffer.
   - Early-out in `SetBloodied` / `SetWet` when the clamped value equals `_blood` / `_wet`, with a "materials changed"
     flag set whenever `_mats` is rebuilt (disguise, wounds), so new materials still get the value.
   - A cache in ActorView instead would not be exact: `ReplayStage.cs:287` (`SetBloodied(0)`) and `ActorWounds.cs:120`
     (`SetWet(true)`) also write through the rig, so the check belongs in ActorRig.
10. **Hidden people still animate:** `ActorAnimator.LateUpdate` (`ActorAnimator.cs:480-485`) evaluates and applies the
    full pose (50+ bones) for every person every frame, including people ActorView has hidden because their room is
    culled. That is 15–17 of 19 in a corridor or a small room.
    - Skinning already stops for disabled renderers, but the bone writes do not.
    - Tick hidden rigs at about 4 Hz with accumulated dt, and do one full tick on the frame they become visible.
    - Nothing on screen changes.

### 2.3 Project / render settings (lead — shared files)

- `ProjectSettings.asset:107-108` GPU skinning (§2.2.1).
- `ProjectSettings.asset:193` `StripUnusedMeshComponents: 0` → 1 (Optimize Mesh Data). First check that no shader uses
  a channel only at runtime; toon outline normals live in tangents and are used.
- `QualitySettings.asset:286` `skinWeights 255 → 4`.
- `RP_BASSLINE_URP.asset:85` `m_GPUResidentDrawerMode: 0`. The GPU Resident Drawer mostly helps with repeated
  mesh+material pairs. The mansion is mostly unique merged meshes, so expect little. Low priority, try after the
  culling fixes.

### 2.4 Kernel — `Sim/World/Nav.cs` (outside every listed ownership; patch ready)

`perf/Nav.PathfinderFast.cs` is a drop-in `PathfinderFast.Find`. It compiles against the current tree (`Sim/` plus
this file build with 0 errors). It keeps the same A*: the same step costs, heuristic, re-opening without a closed set, and
the open list ordered by the same total order (f, then `((floor+8)<<32)|cell`), so ties pop in the same sequence. The
string pull is the same.

What changes:

- Per-layout arrays with generation stamps instead of 4 dictionaries per call.
- An indexed binary heap instead of `SortedSet<(float,long)>`.
- Stair links built once per set of nav grids.
- **A memo of exhaustive failures:** an A* that empties its open list proves the goal cell unreachable from the start
  cell for that set of passable doors. The same (grids, start cell, goal cell, passable-door bitset) then fails without
  searching.

Verified in one build against the old `Find`, 2 days per seed, final saves:

| seed | kernel B/tick old → pooled → +memo | Find ms (A/B, one process) old → pooled → +memo | step µs/tick | Find alloc/call | memo hits | save |
|---|---|---|---|---|---|---|
| 20260926 | 67,139 → 5,795 → 5,742 | 5,144 → 2,898 → 1,434 | 111.0 → 72.4 → 53.7 | 630.8 KB → 2.6 KB | 3,092 of 5,655 calls | IDENTICAL |
| 777 | 43,539 → 5,295 → 5,295 | 5,680 → 2,084 → 1,000 | 125.7 → 57.8 → 39.6 | 792.2 KB → 3.4 KB | 220 of 2,815 | IDENTICAL |
| 4242 | 15,544 → 5,051 → 5,051 | 2,051 → 654 → 610 | 66.7 → 35.8 → 34.8 | 188.4 KB → 2.9 KB | 0 | IDENTICAL |

Re-checked at the end of the session against the tree with the §1 fixes (seed 777, 1 day, mode 0 vs mode 2): the same
save `7A2B50BA…`; kernel 53,931 → 4,817 B/tick; step 86.1 → 24.2 µs/tick; Find 2,156 → 392 ms.

**Reusable harness:** `C:/Users/리오/BL23Lab/perf/navab/` (the tree's `Sim/` with `Nav.cs` swapped for an instrumented
copy). Run `dotnet run -c Release -- 2 20260926,777,4242 0,1,2 [staged]`. Every mode must print the same `save` hash per
seed. The harness carries its own copy of `Nav.cs` as of 2026-09-27; refresh `Nav.instrumented.cs` if `Nav.cs` changes.

Apply:

1. Put the class in `Sim/World/Nav.Fast.cs`.
2. Make `Pathfinder.Find` return `PathfinderFast.Find(L, from, to, doorCost, maxExpand)`.
3. Keep the old body as `FindReference` for an A/B mode in SimTests.

The class uses static state and is not re-entrant. The kernel is single-threaded, and the background save task only
packs and writes (serialisation stays on the main thread), so this is safe today.

### 2.5 Stuck NPCs retry the same impossible walk (cast-voice: LifeAI/Routines; layout owner for 2)

This is a behaviour bug, and the biggest part of the kernel's cost on 2 of 3 seeds.

1. **Stale "believed locked" doors never expire.**
   - Seed 20260926, day 1 23:05: P04 and P16 are in the indoor pool (실내 수영장) when the night-policy doors lock. P04
     tries `d15` → `k.DoorLocked[15] = true`.
   - `DoorCostFor` (`Movement.cs:133-146`) makes a believed-locked door impassable, so the path search never walks them
     back to find it unlocked in the morning.
   - P04 fails `sleep` 962 + 932 times that night, then from the kitchen/dining **all of day 2** (`tea`, `restore`,
     `eat`): 3,292 "no path" failures in 2 days.
   - Suggested fix:
     - Store the clock with the belief and treat old beliefs as a high cost instead of −1.
     - Or clear `DoorLocked` beliefs on the morning unlock (`y_morning`).
     - And after 2 "no path" failures on the same activity, back off `NextThink` 10–15 min or pick a fallback (knock /
       sleep where they are).
   - This is a behaviour change (the NPCs get unstuck), so the owner decides.
2. **Unreachable spots inside a room** (seed 777): P15/P06 fail "no path from 기록실 to 기록실" about 250 times.
   - Both archive doors are open and not believed locked, so furniture splits the archive and the chosen spot sits in a
     pocket reachable only through another room.
   - The spot check (`NavGrid.InMain`) accepts any region that touches *a* door.
   - Validate spots against the room's own main region at layout build, or blacklist a spot for an actor for N minutes
     after a no-path.

### 2.6 clues-and-qol — `Game/UI/Hud.cs`

`Update()` (`:120-140`) rebuilds `_clock.text` and `_place.text` with `$"…"` formatting every frame (`:131-132`).
`UpdateInfo()` (`:177+`) makes a `new List<string>`, formatted lines, `string.Join` and LINQ every frame. TMP skips the
mesh rebuild when the text is equal, but the strings are garbage (roughly 1–3 KB/frame).

Rebuild only when an input changed:

- `S.Tick` (the clock only moves with ticks, ≤10 Hz)
- the player's room
- `S.Phase`
- `_firm` / `_caseLine2`
- the gathering list version

For example, keep the last `(tick, room, phase)` and return early.

### 2.7 trial-reforge / courtroom

The courtroom window allocates **660–1,051 KB/s with the world frozen** (two runs, estimated). Kernel = 0, so this is
all trial presentation.

- `Trial/Gothic.cs:153` `FlameGraphic.Update() { SetVerticesDirty(); }` rebuilds each candle's mesh and re-batches its
  canvas every frame. Put the flames on their own nested `Canvas`, or animate the flicker in the material (UV/time) and
  dirty only when `Life` changes.
- Then profile the trial scene in the editor with Deep Profile: the release player exposes no GC counter.

### 2.8 time-on-demand — ItemView / saves

- `ItemView.LateUpdate` (the time-on-demand block) runs on 217 items every frame for a 0.4 s hidden-state poll and the
  slot/hop follow. Moving the poll into one round-robin loop in `WorldPresenter.Sync` (a few items per frame) and
  enabling `ItemView` only while `_slot != null || _hopT >= 0` would remove about 200 LateUpdate dispatches per frame.
- Saves: 5.5 MB JSON, 86 ms (.NET) and 42 MB allocated per `SaveStore.Serialize` at day 3. The per-person
  `Knowledge.Sightings` (cap 2,500) and `Heard` (cap 1,500) lists are about half of it (campaign test:
  sight = 1.76 MB, heard = 1.07 MB).
  - Even `WriteInBackground` serialises on the caller's thread: `SaveStore.Prepare` runs there and only gzip + write go
    to the task. So every autosave and manual save (`Session.cs:445-455`) costs the main thread about 150–350 ms under
    Mono plus a large GC.
  - Options:
    - Lower the caps or thin old sightings. This needs the owner, because testimony reads them.
    - Serialise behind the transition fades that already cover autosaves.
  - Owner: kernel State (unlisted → lead) with time-on-demand (Session).

### 2.9 Audio — already done (149 MB; not revisited)

### 2.10 Kernel leftovers after §2.4 (5.0–6.2 KB/tick)

- `LifeAI.ChooseLife` (`LifeAI.cs:37+`), 2.2–3.1 KB/tick: a `List<(double, Func<Activity>, string)>` + a closure per
  candidate + `$"ate:{a.Id}:{S.Day}:{slot}"` / `cooked:` keys + `Activities.All.Any(...)` per think.
  - Keep a reusable candidate list of (score, kind enum).
  - Precompute a per-RoomType "has a non-meal activity" table.
  - Cache the flag keys per (actor, day, slot).
  - Owner: cast-voice.
- `Simulation.cs:169-184`: `Guard("crowd", Crowd)` etc. allocate a delegate per call (13 × 64 B ≈ 0.8 KB/tick). Cache
  them as `readonly Action` fields. Owner: lead.
- `Perception.Sound` (`Perception.cs:160`, a free file, left alone): a `Dictionary` + `Queue` per sound (≈3 KB per sound, ≈0.07 sounds/tick).
  They can be pooled only with a re-entrancy guard, because `OnHeard` can raise another sound. Low priority.

---

## 3. A/B levers (probe `perf_levers`, seed 777)

How it ran: `AutoProbe.PerfLevers.cs`, one build (the tree at 19:22, including the fixes in §1). The kernel is paused,
so the scene holds still. Each window is 1 s warm-up + 3 s of counters. There is one `~ref` window, then one change at
a time, each undone afterwards. Values are the per-frame average; % is against `~ref`. Screenshots are in
`probes/perf_levers/NN_perf_<scene>~<lever>.png`.

| scene / lever | draw calls | SetPass | triangles | shadow casters | skinned upload | visible renderers | shot |
|---|---|---|---|---|---|---|---|
| **hall ~ref** | 21,034 | 2,889 | 37.28 M | 3,161 | 40.1 MB | 1,533 | 02 |
| hall ~noreflect | 11,948 (−43 %) | 1,472 (−49 %) | 21.79 M (−42 %) | 3,161 (0) | 0 | 1,460 | 03 |
| hall ~charshadowoff | 20,443 (−3 %) | 2,879 (0) | 32.73 M (−12 %) | 2,582 (−18 %) | 0 | 1,532 | 04 |
| hall ~lodbias1 | 21,028 (0) | 2,884 (0) | 30.08 M (−19 %) | 3,160 (0) | 26.0 MB (−35 %) | 1,532 | 05 |
| hall ~shadows0 | 17,958 (−15 %) | 2,858 (−1 %) | 30.77 M (−17 %) | 0 (−100 %) | 0 | 1,532 | 06 |
| hall ~hallcull (2,447 renderers hidden) | **9,322 (−56 %)** | 2,718 (−6 %) | 27.25 M (−27 %) | 1,472 (−53 %) | 0 | 588 | 07 |
| **dining ~ref** (16 people) | 8,263 | 2,216 | 30.69 M | 2,406 | 45.3 MB | 482 | 09 |
| dining ~noreflect | 5,287 (−36 %) | 1,165 (−47 %) | 20.71 M (−33 %) | 2,409 (0) | 0 | 476 | 10 |
| dining ~charshadowoff | 7,388 (−11 %) | 2,201 (−1 %) | 21.90 M (−29 %) | 1,532 (−36 %) | 0 | 482 | 11 |
| dining ~lodbias1 | 8,273 (0) | 2,220 (0) | 21.55 M (−30 %) | 2,408 (0) | 28.1 MB (−38 %) | 482 | 12 |
| dining ~shadows0 | 5,898 (−29 %) | 2,147 (−3 %) | 20.12 M (−34 %) | 0 (−100 %) | 0 | 422 | 13 |
| **dialogue ~ref** (in the dining room) | 9,163 | 2,651 | 33.61 M | 2,616 | 49.4 MB | 489 | 19 |
| dialogue ~noreflect | 5,835 (−36 %) | 1,377 (−48 %) | 22.69 M (−32 %) | 2,617 (0) | 0 | 460 | 20 |
| dialogue ~charshadowoff | 8,145 (−11 %) | 2,643 (0) | 23.49 M (−30 %) | 1,584 (−39 %) | 0 | 491 | 21 |
| dialogue ~lodbias1 | 9,176 (0) | 2,660 (0) | 32.42 M (−4 %) | 2,617 (0) | 46.4 MB (−6 %) | 491 | 22 |
| dialogue ~shadows0 | 6,585 (−28 %) | 2,583 (−3 %) | 21.62 M (−36 %) | 0 (−100 %) | 0 | 420 | 23 |
| dialogue ~far25 (ConversationCamera 140 → 25 m) | 7,878 (−14 %) | 2,385 (−10 %) | 32.12 M (−4 %) | 2,541 (−3 %) | 0 | 464 | 24 |

What the screenshots show:

- **hallcull** and **far25** look the same as `~ref`.
- **lodbias1** (people at 9–14 m on LOD1) and **charshadowoff** are very hard to tell apart in a crowd.
- **noreflect** loses the faint gloss on the parquet and marble.
- **shadows0** visibly flattens the furniture: under the tables and chairs.

So the recommendations keep the reflection and the shadows, just cheaper.

Managed allocation in the same run:

- Hall with the house running: 1,385 KB/s.
- The same hall with the kernel paused: 73 KB/s.
- Kernel-paused lever windows: 16–73 KB/s.
- Courtroom with the world frozen: 1,051 KB/s. All of this is trial presentation.

These are estimated from mono used-size increases. The player does not expose "GC Allocated In Frame", so small values
are noisy.

**The other windows** (the house running at 1x):

| scenario | draw calls | SetPass | triangles | shadow casters |
|---|---|---|---|---|
| corridor_long | 6.3 k | 605 | 8.4 M | 429 |
| library_dense | 9.3 k | 1,082 | 12.6 M | 703 |
| greenhouse | 3.0 k | 489 | 3.5 M | 658 |
| basement | 2.3 k | 224 | 3.4 M | 0 |
| courtroom_trial | 520 | 393 | 3.1 M | 71 |

- library_dense, greenhouse and basement are within ±3 % of `perf_before`. corridor_long has −11 % tris and −15 % SetPass: the mansion changed in between, and it has no people in it.
- discovery_film and crime_scene repeat poorly (the camera crosses rooms), as noted before.
- The probe ran all scenarios with **0 exceptions** (`probes/perf_levers_run.txt`).

---

## 4. Re-measure

- **Unity:** `bash C:/Users/리오/BL23Lab/build_probe_retry2.sh <name> perf BL23_perf 777`. Output goes to
  `probes/<name>/perf_summary.md`, `perf_*.json` and the `PERF` / `PERFAB` lines in `probe_log.txt`.
  - Compare draw calls, SetPass, triangles, shadow casters and `vbUploadBytes` against `probes/perf_before`.
  - `discovery_film` and `crime_scene` need 2 runs per build (poor repeatability).
- **Kernel:** `cd Tests/BL23/SimTests && dotnet run -c Release -- tickcost 2` (per-stage µs and B/tick, staged vs plain
  save).
- **Build size:** `bash C:/Users/리오/BL23Lab/tools/build_size.sh probes/<name>/unity_build.log`.
