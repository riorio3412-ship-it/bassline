---
name: owner-models-face-approach-b
description: "BASSLINE owner chose face approach B (2026-09-28) for their Tripo character models, and will resend regenerated models; keep their face design, replace only the painted eyes and mouth with real 3D eyes, lids, mouth and shape keys"
metadata:
  node_type: memory
  type: project
  originSessionId: 2927cf85-ff74-4e21-abc8-f9c9d50f8a0c
  modified: 2026-09-28T00:38:51.927Z
---

**Decision (2026-09-28):** the owner's AI-generated GLB characters have eyes and mouth painted into the texture, one fused mesh, no bones and no blend shapes, so face animation breaks. The owner chose **approach B**:
- Keep their face shape, skin, nose and hair exactly.
- In Blender, replace the painted eyes and mouth with eyeballs plus lids. Copy the iris colour, size and shape from the original texture.
- Add a mouth interior, teeth and a tongue.
- Add shape keys: blink L/R, jaw open, vowels A/I/U/E/O, smile, frown, brows.
- Drive the shape keys from ActorFace and turn off the painted overlays.

**The owner will RESEND regenerated models** made with the Tripo guidance: A-pose, bangs clear of the eyes, no capes, flat lighting, Segmentation v2 Detailed + Part Completion, retopology to 30–60k, plus a face close-up. Don't start B on the old GLBs; preparing the scripts is fine.

**Why:** the owner's top concern is that their own face design must not change ("내가 만든 모델의 얼굴이 달라지는거 아닐까?").

**How to apply:**
- Always show a side-by-side of ONE character (original vs processed; neutral, blink, talk) and get approval before processing the rest.
- Details: HANDOFF.md, item "오너 3D 모델 얼굴".

Related: [[horror-presentation-directives]], [[handoff-pointer]]
