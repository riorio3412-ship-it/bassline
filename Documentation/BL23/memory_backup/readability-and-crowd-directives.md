---
name: readability-and-crowd-directives
description: "BASSLINE user feedback 2026-09-27 afternoon — plain clock times (no bell counting), per-person movement board, few evidence cards, visible NPC purposes, no identical crowd poses, rich rooms, deeper murder sim"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 2927cf85-ff74-4e21-abc8-f9c9d50f8a0c
  modified: 2026-09-27T06:35:08.752Z
---

User feedback (2026-09-27, ~14:05–14:20):
- "다들 같은 자세로 시체 조사하는거 좀 이상해보여" — never let a crowd do the same thing in the same pose; cap close examiners (≤2, different poses), others keep distance and react in character.
- "시간당으로 쪼개니까 보기가 너무 불편" — no hour-bell counting ("네 번째 종과 다섯 번째 종 사이"); speak plain times ("오후 4시 반쯤"); timeline as a per-person board, not an hourly list.
- "증거들이 너무 많아서 복잡" — few, meaningful clue cards; overheard chatter is not filed; merge repeats.
- "캐릭터들이 움직이는 목적도 불분명" — show what NPCs are doing/where they go; they voice intent.
- "각 방들의 내부도 … 비어있는 느낌" — dense room dressing.
- "살인시뮬레이션이 아직 부족 — 트릭·무기·살인 방법" and "일상에서 할 것들도 부족"; "기획서에 세세하게 다 있어" (planning PDFs in C:/Users/리오/Downloads: BASSLINE_v2.2_통합기획_개발명세.pdf, BASSLINE_v2.2_추가규칙_반복플레이_확장.pdf, BASSLINE_통합_게임_기획서_v1.1.pdf).

- 15:20 follow-up: "증거 시스템이나 단서들이 아직도 복잡해" even after the filters above; "편의성은 꼭 챙겨줘야해"; "시스템적인 것들도 네가 판단해서 더 발전시켜봐" — target a handful of named key clues per case (one card per witness, plain one-line meaning, jargon hidden), trial pickers show only relevant clues, and always add QoL (objective hints, map markers, backlog, autosave etc.) without being asked.

**Why:** the user judges by readability and liveliness like a commercial game; clutter, cryptic time phrasing and uniform crowd behaviour read as broken.
**How to apply:** ClockFmt speaks plain times; Evidences.FromStatement filters/merges; NoteUI.Timeline is the 동선표; IntentLines for NPC purpose; Cases.Investigate caps examiners; murder content per the PDFs (GapReport_Mystery.md). Related: [[first-person-motions-clues]], [[commercial-quality-vertical-slice]]
