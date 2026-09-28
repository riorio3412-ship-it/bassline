---
name: handoff-pointer
description: "Where to resume BASSLINE BL23 work: Documentation/BL23/HANDOFF.md (§0.5 owner goal verbatim, §3.00 stop point) — updated 2026-09-28"
metadata:
  node_type: memory
  type: project
  originSessionId: 2927cf85-ff74-4e21-abc8-f9c9d50f8a0c
  modified: 2026-09-28T00:29:47.248Z
---

The session of 2026-09-27/28 ended with every agent and workflow stopped mid-work by the monthly usage limit.

**Where things are:**
- `Documentation/BL23/HANDOFF.md` (project root C:\Users\리오\OneDrive\Desktop\BASSLINE) is the single handoff:
  - §0.5 has the owner's current goal VERBATIM, plus the 09-28 addition about a Danganronpa-like discussion feel;
  - §3.00 lists where each agent and workflow stopped and which spec doc to restart from;
  - §3.0 lists the latest work.
- The owner's ready-to-paste first message is in `Documentation/BL23/NEW_SESSION_PROMPT.md`.
- Last verified state (09-28 06:xx): GameCompile 0 errors; campaign faults=0 IDENTICAL; play build `Builds/BL23_play3` with 0 probe exceptions.
- Build+probe: `C:/Users/리오/BL23Lab/build_probe_retry2.sh <name> <mode> <buildDir> rand`, following the Unity lock protocol.

**Why:** the user continues in new chats and wants their requirements carried over exactly.

**How to apply:**
- Read HANDOFF §0.5 first and treat it as the requirements baseline.
- Workflow runIds can't be resumed across sessions, so restart work as new agents from the spec docs (TrialReforge.md, MurderFoundation.md, CaseTruthApi.md, DailyLifeDesign.md, ViolenceMotionContract.md, VoicePackGuide.md, CharacterPipeline_Current.md).
- Update HANDOFF.md before the session ends.

Related: [[owner-goal-physics-initiative-debate]], [[commercial-quality-vertical-slice]], [[test-after-system-complete]]
