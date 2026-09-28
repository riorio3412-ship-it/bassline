---
name: trial-as-hypothesis-debate
description: "BASSLINE core vision (2026-09-27 19:20): audacious, cunning murders in shared moments + a 심판 driven by residents' competing theories and a desperately lying culprit — not a clue-vs-clue objection loop"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 2927cf85-ff74-4e21-abc8-f9c9d50f8a0c
  modified: 2026-09-28T00:29:57.880Z
---

The user explained what fun they intend, using a Danganronpa 2 ch.1-style case as an example (reference only, never recreate that case as a set). Two things matter:
- **The murders need audacity and cunning.** A killing happens during a shared moment: a blackout caused while everyone is together, the target marked so they can be found in the dark, blood hidden under a tablecloth or rug, the crowd's confusion used. Each witness perceives different fragments.
- **The 심판 is a debate of competing hypotheses.** An open question such as "왜 시신이 탁자 밑에?" gets different theories from different residents, each from their own perceptions and personality (hid from the dark / dropped something / moved after death). Theories are tested, collapse and evolve.
- **The culprit fights desperately:** cover story, lies, alternative theories, a scapegoat, twisting testimony, feigned emotion, then cracks gradually. The player wins by catching contradictions in what people SAY. Cards only test theories.

The user explicitly dislikes the current "one resident presents a clue, another presents a clue, objection, objection" flow ("단서 반대 반대").

Follow-up (same evening): "저 사례에만 국한되지 않고 여러가지를 만들어내면 좋겠어. 이 게임은 계속 반복해서 플레이하는 클로즈드 서클 게임이니까" — the example is one illustration; generate endless variety (shared moments × schemes × concealment, case-derived open questions) with cross-run novelty memory, because the game is a replayed closed circle.

2026-09-28 addition, which the owner asked to carry into new sessions: daily life and the 심판 must have the Danganronpa FEEL. It should read as a real discussion ("토론하는 느낌"), not a game system. The spec in TrialReforge.md must be implemented to feel like conversation first.

**Why:** this is the fun the user designed the game around. Danganronpa-level trials feel alive because of people's reasoning and lies, not the evidence items.

**How to apply:**
- Every trial or murder design must have per-resident beliefs, a theory generator, a culprit lie planner, a debate director (question → theories → tests → next question), and group-setting darkness murders.
- The binding text is in Documentation/BL23/workflows/vision2.txt.
- Related: [[no-danganronpa-copy-gothic-style]], [[horror-presentation-directives]], [[first-person-motions-clues]]
