---
name: test-after-system-complete
description: "BASSLINE owner (2026-09-27 22:50) — don't test often; open/run the game to check for errors only after a whole system is finished"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 2927cf85-ff74-4e21-abc8-f9c9d50f8a0c
  modified: 2026-09-27T13:35:38.020Z
---

Don't run in-game tests (Unity builds, probes, playing the build) frequently. Finish one whole system first, then open the game once and check it for errors. Cheap compile checks (GameCompile "오류 0개") and headless SimTests are fine while coding.

**Why:** the owner felt that frequent Unity builds and probe runs slowed progress on the actual features they asked for.
**How to apply:** batch verification per system. Tell subagents and workflows the same rule: no Unity probe per small change, and one build and probe at the end of their system. Related: [[commercial-quality-vertical-slice]], [[priority-fun-richness-polish]]
