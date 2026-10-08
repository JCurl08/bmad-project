---
title: 'Fragments and face repair'
type: 'feature'
ticket: '8'
created: '2026-10-08'
status: 'draft'
baseline_revision: ''
route: 'full'
route_source: 'auto'
risk: 'high'
review: ''
review_source: ''
lenses_ran: []
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
  - '{project-root}/_bmad-output/initiative-video-game/epic-world-worth-exploring/puzzle-difficulty-research.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user said: "Each face should have 1-6 fragements to collect (the town face awards you with the 1 fragement and the other faces can randomly have 2-6 of their fragments available to collect, each with a unique number like a dice). Once you collect all the fragements the village leader for that face will help you repare it and it gives you some sort of benefit for the final boss fight, one reduces the number of things to unsort, one increase the gap that you have to push them through, one makes them slightly attracted to you so it is easer to push, one makes the boss slower at resorting." The user picked the two other benefits: +1 heart, and slower, rarer shots.

**Approach:**
1. **Die numbers.** Each run rolls a die number per face: Town is 1, and the other five faces get 2–6, shuffled per run. Use a new RNG stream.
2. **Fragments.** The face numbered k has k fragments, numbered 1..k and shown like die pips. They sit in the fragment slots of the roadblock rewards from 4.7. Town's single fragment is awarded by the Mayor's quest (talk, then receive it), so the Town face is repaired once you hand it back.
3. **The leader's quest.** Each leader's quest line (4.5) names the count, in voice. Returning with all k fragments and talking to the leader repairs the face. The repair shows as a visible change in the town (a lit statue or a banner) and on the map.
4. **Boss benefits.** Each repaired face grants one benefit. The default mapping is fixed per face theme, and the plan can choose it:
   - fewer particles to un-sort;
   - a wider membrane gap;
   - particles slightly drawn towards the player;
   - the Demon restores order more slowly;
   - +1 heart for the fight;
   - the Demon's shots are slower and rarer.
5. **HUD and map.** They show the fragments collected per face and which faces are repaired.

Note from the 4.5 review: read `TownPopulation.Leader` and `TownSquares.LeaderOf(face)` fresh each time. The reveal re-populates the town, so cached references go stale.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] Roll die numbers; place fragments in reward slots; Town's single fragment comes from the Mayor.
- [ ] Leader quest (count, hand-in, repair state, a visible repair in the town and on the map).
- [ ] Boss benefits applied in CoreArena/MaxwellDemon from the repaired faces. The no-benefit skill check stays at 60–120 s.
- [ ] HUD and map fragment display.
- [ ] Tests:
  - on seeds 1–50, every fragment is reachable (sweep), Town is always 1, and the other die numbers vary;
  - a hand-in repairs the face;
  - each benefit measurably changes its arena parameter;
  - the suites and the sweep pass.

## Routed from the 4.7 review

- **(medium) Fragment slots pay nothing.** Each face has 6 numbered fragment slots, but 4.7 only records them. Fill slots 1..k on the face numbered k. Slots above k must give something useful (Jumbles\* or nothing visible): no empty-looking shards.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
