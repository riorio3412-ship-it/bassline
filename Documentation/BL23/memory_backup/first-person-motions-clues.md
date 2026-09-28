---
name: first-person-motions-clues
description: "BASSLINE presentation rules from the user (2026-09-27): first-person exploration, protagonist on screen only in dialogue/trial, every action has a motion, not every line is a clue"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 2927cf85-ff74-4e21-abc8-f9c9d50f8a0c
  modified: 2026-09-27T09:20:49.397Z
---

User directives (2026-09-27, in order):
1. "모든 행동에 모션이 전부 있으면 좋겠어" — every action has an animation, player and NPCs alike (e.g. resting on a chair = pull the chair out, sit; knife swing, drinking water…).
2. "3인칭은 제외해. 1인칭으로 진행해." then clarified: "대화하거나 재판 때만 주인공이 나오는거야. 탐색 때는 3인칭 제외" — exploration is first person only (no third-person toggle); 민혁 is shown on screen only in conversations and the trial. Exploration-time staged moments (activities, discovery) stay first person (full-body FP: body visible from its own eyes, head hidden).
3. "모든 대화가 단서가 되니까 너무 복잡하게 느껴져" — ordinary talk must not become evidence; only suspicious facts in daily life and case-relevant testimony during an investigation are filed.
4. 18:20 — clue DECK per case: about 10 cards, about 5 real + 5 fake (red herrings); testimony only as a card when it is one of the 10; every evidence card shows a real render of the thing as found (e.g. the broken teacup on the floor); easy to view/use everywhere. Also: optimize periodically; keep improving models; make the trial fun and convenient.
5. Looking down in first person must not show a black coat blob (body renders shadow-only when looking down).

**Why:** commercial feel; clarity; the user found the evidence flood confusing and third-person exploration unwanted.
**How to apply:** keep PlayerController first-person (BodyEye/BeginScript), ActorRig.SetHeadHidden for the player outside dialogue/trial, DialogueUI OTS shots may show 민혁; Evidences.Worthwhile gates statement cards; new actions must come with a motion (kernel Anim + ActorView/animator). Related: [[commercial-quality-vertical-slice]]
