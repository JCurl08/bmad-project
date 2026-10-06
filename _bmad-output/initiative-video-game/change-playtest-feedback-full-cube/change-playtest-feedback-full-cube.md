---
type: sprint-change-proposal
title: "Playtest feedback: full cube, clearer world, subtle science"
initiative: initiative-video-game
date: 2026-10-06
mode: batch
status: approved
scope: moderate
---

# Sprint Change Proposal: playtest feedback, full cube, clearer world, subtle science

## 1. Issue summary

**Trigger:** the user's first hands-on playtest of Epic 1 (stories 1.1–1.13 and 1.15 built; the release, 1.14, is pending). The 1.15 readability pass was accepted ("I think it is good"). The playtest raised new requirements and a scope increase:

- **Isotope:** the Chemistry item is confusing. The timer, how to use it and how to get it are all unclear. The user wants "an actual item, not a pickup. Maybe the item could be a specific stage but the lore explains that they all transform into each other."
- **Characters:**
  - The shape people should look more human: a round face, a triangular body and rectangular legs.
  - The finches "are a little ugly".
- **Colour:** "could still be more colourful". Regions should use Rubik's-cube colours: Biology green, Chemistry red, Physics blue.
- **Map:** add a map. The user chose a cube-net pause map.
- **Scope increase:** "a full cube is feasible within the first week… Each region is 3x3 and we have 6 faces."
- **Subtle science:**
  - The analogy must be subtle. Characters must not know they stand for a science theme.
  - Better names are wanted, but are deferred to a later naming brainstorm. Until then, any placeholder name that appears in in-game text is marked with `*`.
- **Also found during the playtest:**
  - The player can get locked in the boss arena, because there is no exit except winning, dying or debug keys.
  - The dev keys F8 (leave the arena), F9 (rest) and F10 (new run) were added and are not yet tested.

**Issue type:** new requirements from the stakeholder, plus a scope increase. Nothing built is technically broken.

## 2. Impact analysis

**Epic impact**
- **Epic 1 (playable cube)** grows:
  - The Math (CAP-7) and Earth & Atmosphere (CAP-8) faces move in from Epic 2.
  - The 3×3 face size moves in from Epic 2.
  - A map is added (new CAP-26).
  - The isotope (CAP-5) is redesigned.
  - A look and tone pass is added.
  - The release (1.14) stays last.
- **Epic 2 (varied runs)** shrinks to CAP-14 to CAP-17: the town draft, demon phases and multiple wins, difficulty scaling, and improved abilities.
- **Epic 3 (stretch)** is unchanged.

**Built work affected**
- **The 3×3 setting** already exists, built in 1.2 as a single face-size setting. However:
  - Town has only 4 hand-built modules (deferred item: 9 are needed at 3×3).
  - Each face plan (Biology, Chemistry, Physics), the trial fit, the boulder reach and the hidden-currency placement must be re-verified at N=3.
  - The 1.13 determinism signatures must be re-recorded, because the layout intentionally changes.
- **Chemistry (1.9)** is reworked:
  - Remove the timer and the dispenser.
  - The isotope becomes a run-rolled variant item: Glowing, Unstable or Lead, reusing 1.8's variant system.
  - Every stage is used by attacking (one rule for all items).
  - Gates for the other stages become optional, as with the beak.
  - The sweep rules change: "dispenser-first" goes away, and "optional gates guard nothing required" applies to Chemistry.
- **NPC art (1.15):** shape people and finches are redrawn.
- **Region colours:** floors and accents change. Proposed Rubik's mapping: Town white, Biology green, Chemistry red, Physics blue, Math yellow, Earth orange.
- **In-world text:** all NPC, HUD, trial and boss text is rewritten to drop explicit science words and scientist names. Placeholder proper names get a `*`.
- **Boss arena:** gets a real exit.

**Artifact conflicts**
- **Spec** (`spec-entropy-cube.md`):
  - CAP-1 changes: faces are 3×3 at Must tier.
  - CAP-5 is rewritten.
  - CAP-7 and CAP-8 move to Must.
  - New: CAP-26 (map) and CAP-27 (arena exit).
  - New constraints: subtle-science text rule, placeholder `*` rule, Rubik's colour mapping.
- **lore-and-tone.md:**
  - The current voice rules say "earnest scientist-deities" and include a Newton cameo.
  - Change these so the characters' deities and heroes are in-world figures, and never named as scientists or described with science vocabulary.
  - The analogy lives in mechanics and visuals only.
- **faces-and-races.md:**
  - The deity column becomes "inspiration (designer-only)".
  - The Chemistry row's item changes to the rolled stage item.
  - Shape people become human-shaped.
- **scope-tiers.md:** rebalance the tiers.
- **Initiative, epics and tickets:** move covers, and add stories.
- No architecture or UX documents exist. `stack.md` is unaffected apart from a note on N=3.

**Technical impact**
- More content: 6 faces × 9 = 54 screens.
- Longer seed sweeps.
- Two new face systems.
- One rework.
- Test runtime grows; PlayMode is already about 5 minutes.
- No change to the WebGL target.

## 3. Recommended approach

**Direct adjustment (option 1)**, adding stories within Epic 1.

- **Option 2, rollback:** not useful. Everything built stays valid. Chemistry is reworked in place.
- **Option 3, reduce scope:** the user explicitly asked to increase scope.

**Effort:**
- Seven new or changed stories at roughly 1 to 1.5 hours of agent time each, plus the user's playtests.
- That is about 8 to 12 hours of build time, which can run unattended, plus WebGL and release.
- This fits the "first week" goal, ending Sunday 2026-10-11.

**Risk: medium.**
- Running all three existing faces at 3×3 is the main unknown: placement density, trial fit and timed-door distances.
- It goes first, so problems surface early.

## 4. Detailed change proposals

### 4.1 Stories (Epic 1, `epic-playable-cube/tickets.toml`)

Build order after the change: **1.16 → 1.17 → 1.18 → 1.19 → 1.20 → 1.21 → 1.22 → 1.14**.

| New entry | Title | Covers | After | What it delivers |
|---|---|---|---|---|
| 16 | Dev keys | CAP-11 (tooling) | 15 | Already written. F8 leaves the arena, F9 rests, F10 starts a new run, with a test. Test it and commit it. |
| 17 | Full cube at 3×3 | CAP-1, CAP-2, CAP-3 | 16 | Face size set to 3. 9 Town modules (clears the deferred item). Every face plan and sweep rule passes at N=3. Determinism re-recorded. Science faces stay sealed until 1.19 and 1.20. |
| 18 | Isotope as a stage item | CAP-5 | 17 | Isotope = run-rolled variant (Glowing, Unstable or Lead), with no timer and no dispenser. Used by attacking: glowing lights dark rooms, unstable breaks cracked walls, lead weighs down plates. Gates for other stages are optional extras. Trial redesigned around the rolled stage. One lore line about stages transforming. |
| 19 | Math face | CAP-7, CAP-25 | 17 | Cross-every-bridge-once puzzles with a solvability validator, a trial, enemies, and shape people with human-like parts (round face, triangular body, rectangular legs). Uses the `IFaceContent` hook. |
| 20 | Earth & Atmosphere face | CAP-8, CAP-25 | 17 | Weather hazards (wind push, snow slide, freezing-rain slow), quake crevices, a calm centre screen, the weather wand rolling one phenomenon per run, a trial, enemies, and terrestrial and avian dinosaur NPCs. |
| 21 | Cube-net map | CAP-26 (new) | 19, 20 | A pause key (M or Tab) opens an unfolded cube showing each face's 3×3 screens. Visited screens appear in their region colour. It also marks you, Town, discovered gates, items, trials and core portals. It updates with the shuffle. |
| 22 | Look and tone pass | CAP-9, CAP-11, CAP-27 (new) | 18, 19, 20 | Rubik's region colours plus a more colourful palette. Prettier finches. Shape people redrawn, if 1.19 hasn't already. All in-world text made subtle-science with `*` placeholders. A boss-arena exit door the player can leave by when the fight hasn't been won. |
| 14 | First itch.io release | (unchanged) | 22 | Moved to after 22. |

The **Math and Earth stories run in parallel** with the isotope story in principle. The build still goes one at a time, in the order above.

### 4.2 Spec (`spec-entropy-cube/spec-entropy-cube.md`, through `bmad-spec`)

```
CAP-1  OLD: 6 faces × N×N screens (2×2 at Must tier, 3×3 at Should tier)
       NEW: 6 faces × 3×3 screens at Must tier

CAP-5  OLD: Chemistry: re-obtainable decay item with half-life stages…
       NEW: Chemistry: each run rolls one stage item (Glowing, Unstable, Lead), kept for the run and used
            by attacking: glowing lights dark rooms, unstable breaks cracked walls, lead weighs down plates.
            In-world lore: the stages transform into one another between runs.
       success: each stage solves its gates; the other stages' gates are optional and guard nothing required.

CAP-7, CAP-8  OLD: [Should]   NEW: [Must]

CAP-26 (new) [Must] intent: a pause map shows the cube as an unfolded net of 3×3 faces with visited
       screens, the player, Town, discovered gates/items/trials and core portals.
       success: the map matches the run's real layout for the visited screens on any seed.

CAP-27 (new) [Must] intent: the player can leave the core arena on foot while the fight is not won.
       success: walking out of the arena exit returns the player to the face they came from; the fight resets.
```

**New constraints**
- In-world text never names a real scientist or uses explicit science vocabulary for the face themes (e.g. "isotope", "radioactive", "relativity", "evolution", "niche"). The science analogy lives in mechanics and visuals only.
- Proper names in in-game text that are still placeholders carry a trailing `*` until the naming brainstorm replaces them.
- Region colours follow a Rubik's cube: Town white, Biology green, Chemistry red, Physics blue, Math yellow, Earth & Atmosphere orange.

**Scope tiers**
- Must gains CAP-7, CAP-8, CAP-26, CAP-27 and the 3×3 size.
- Should becomes CAP-14 to CAP-17.
- The cut order is updated.

### 4.3 Spec companions
- **lore-and-tone.md:**
  - Replace "earnest scientist-deities" with in-world patron figures, each named with a `*` placeholder.
  - Add the subtle-science rule.
  - The Newton cameo becomes "an apple-bonked sage*" with no name.
- **faces-and-races.md:**
  - Rename the column "Deity" to "Inspiration (designer only, never said in game)".
  - Rewrite the Chemistry item.
  - Describe shape people as human-shaped.
  - Add the Rubik's colour per face.

### 4.4 Initiative and epics (through `bmad-ticket`)
- **`initiative-video-game/tickets.toml`:**
  - Epic 1 covers gain CAP-7, CAP-8, CAP-26, CAP-27.
  - Epic 2 covers lose CAP-7 and CAP-8.
  - Epic 2's `after` needs text drops "face-size setting and sealed-face slots".
- **`epic-playable-cube.md`:**
  - Covers, Description, Done when (3×3 cube with all 6 faces, map, arena exit) and Boundaries.
  - A `Decision:` line for this change.
- **`epic-varied-runs.md`:** remove the Math, Earth and 3×3 work from its Description, Requirements and Done when.
- **`deferred-work.md`:** the Town-at-3×3 item is resolved by 1.17.

### 4.5 Not changed now
- **Naming:** a separate `bmad-brainstorming` session later, for the regions, races, items, patron figures and the game title.
- **Epic 3:** unchanged.

## 5. Implementation handoff

**Scope: moderate.** This needs a backlog reorganisation and spec updates. No architecture replan is needed.

| Role | Responsibility |
|---|---|
| `bmad-spec` (update) | Apply 4.2 and 4.3. Bump the log, re-validate. |
| `bmad-ticket` | Apply 4.1 and 4.4. Run the set check on the new Epic 1 breakdown. |
| `bmad-build` | Build 1.16 → 1.22 in order, then 1.14. Each story is reviewed and verified as before. |
| User | Approve this proposal. Approve each story plan (or let them run under a standing instruction). Play after 1.17, 1.18, 1.21 and 1.22. Run the naming brainstorm when ready. |

**Success criteria**
- A 3×3, six-face cube where every face is playable.
- The isotope is understood without explanation.
- The map is readable.
- No in-world text breaks the subtle-science rule.
- The boss arena can be left on foot.
- All suites pass and the seed sweep is 50/50 at N=3.
- The user's playtest passes, then the itch.io release.

## Checklist status

| Item | Status | Notes |
|---|---|---|
| 1.1 Trigger | [x] | Playtest after 1.15 |
| 1.2 Problem | [x] | New requirements plus a scope increase |
| 1.3 Evidence | [x] | User's words (section 1) and the arena lock-in |
| 2.1 Current epic | [!] | Epic 1 extended |
| 2.2 Epic changes | [!] | Covers moved, 7 entries added |
| 2.3 Future epics | [!] | Epic 2 shrinks |
| 2.4 New or obsolete epics | [x] | None |
| 2.5 Reorder | [x] | Release stays last |
| 3.1 Spec | [!] | Section 4.2 |
| 3.2 Architecture | N/A | None exists |
| 3.3 UX | N/A | None exists. Map and colour UX are specified in the stories. |
| 3.4 Other | [!] | Determinism fixture, test runtime, deferred-work |
| 4.1 Direct adjustment | Viable | Medium effort, medium risk |
| 4.2 Rollback | Not viable | |
| 4.3 Reduce scope | Not viable | The user asked for more |
| 4.4 Selected | [x] | Option 1 |
| 5.x Components | [x] | This document |
| 6.x Review and approval | [x] | Approved by the user 2026-10-06 ("Let's do it.") |
