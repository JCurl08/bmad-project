---
title: 'Character redesign'
type: 'feature'
ticket: '3'
created: '2026-10-07'
status: 'built'
baseline_revision: 'a73e6aa'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/change-playtest-3-world-redesign/change-playtest-3-world-redesign.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user said "The people are still kind of ugly, I think for now we can move the swapable body parts to the next epic and make nice looking characters with a similar design to the main character."

**Approach:** Give each race one hand-made design in the player character's style, at the size set in 4.1:
- shape people (Math)
- finches (Biology)
- Chemistry folk
- Physics folk
- dinosaurs (ground and avian, Earth)
- townsfolk

Each race also gets a visibly distinct **leader variant** (for 4.5), and enemies are left as they are. The swappable-parts system (`NpcPartArt`, the part slots) stays in code behind a switch that is off by default, for epic-varied-runs (CAP-9). Seeded variety may come from a small palette or accessory tint per NPC, but not from swapped body parts.

</frozen-after-approval>

## Boundaries & Constraints

- **Style:** match the player's look: outline, palette weight, proportions and pixel density. Readability tests (contrast and silhouette) are kept or updated, never removed.
- **Data unchanged:** NPC behaviour, dialogue and hints (part-driven hint content keeps working from the data, even if visuals no longer show the parts).
- **Determinism:** the NPC fields change only if visuals are in the signature. Re-record with a note.
- **Testing:** PlayMode in class batches.

## Tasks & Acceptance

- [x] Draw a design per race plus a leader variant, in the player's style.
- [x] Spawn path uses the designs; parts system behind a switch, off by default, with its tests still passing behind it.
- [x] Tests: each race spawns its design; leaders are distinct; silhouette and contrast checks.
- [x] Full verify; screenshots of each race and each leader.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Approach: Option 2, hand-drawn in code** (`Scripts/Cube/Art/NpcDesignArt.cs`). *Assumption:* Option 1 does not fit. Tiny Town has no characters, and Tiny Dungeon's only people are humans (D 84-88, 96-100), several of them already enemies. No tile fits a finch, a mushroom person, an alien, a shape person or a dinosaur.
  - Each design is a 16x16 grid of palette characters, rows top-down. The figure is authored without its outline; code adds the outline: every empty pixel touching the figure (also diagonally), then every empty pixel beside that ring. This gives Kenney's thick two-pixel outline with rounded corners.
  - The style is copied from the player tile (D 112), read pixel by pixel:
    - the outline is its dark plum `#3f2631`;
    - the colours come from Kenney's palette (the player's skin `#f7c282`/`#e19a65`, eyes `#262b44`, browns, and the pack's other colours);
    - the proportions: a head about 8 px wide over a 10-12 px body, two-pixel legs, hands at the sides, and the feet on the bottom edge;
    - the bulk: 186-248 opaque pixels, against the player's 199.
  - *Assumption:* designs are drawn exactly at the player's size (`NpcFactory.DesignSize` = `ContentScale` x `PeopleArtScale` = 2 units, square). Before, the stacked parts were drawn 2.6 units tall. The collider is unchanged: the parts' height, 1.95, by `BodyWidth`. So spots, physics, positions and signatures are unchanged.
- **The designs** (*assumption*: the looks and colours are my choice, kept off each race's own region colour so they read on their home floor):
  - **Townsfolk:** a straw hat, brown hair and a tunic.
  - **Finches:** a round fluffy bird with big glinting eyes, an orange beak, a cream breast and blush. The look follows 1.22's finch.
  - **Chemistry folk:** a spotted mushroom cap, a cream face, a little open "o" mouth (emotionally reactive) and a white coat.
  - **Physics folk:** a green, big-eyed alien with two cyan-tipped antennae, in a jumpsuit.
  - **Shape people:** a round face, a triangular body (light left, dark right) and rectangular legs, as the spec asks.
  - **Ground dinosaurs:** a green, toothy, big-snouted head with orange back spikes, a cream belly and a bandana.
  - **Avian dinosaurs:** a blue feathered raptor with a two-feather crest, a gold beak, accent-tipped wings and thin clawed feet.
  - Dinosaurs take the ground or avian design from `NpcSpec.Variant`, as before.
- **Seeded variety:** each NPC wears one of 3 accent tints, picked by a hash of its salt (`NpcDesignArt.AccentOf`). There is no random draw, so no stream or signature changes. The accent colours:
  - tunic: blue, red, green;
  - plumage: brown, slate, rust;
  - cap: lilac, cyan, green;
  - suit: silver, lilac, amber;
  - body: blue, lilac, mint;
  - bandana: red, blue, grey;
  - crest: orange, gold, pink.
  - *Assumption:* tints only. No accessory swap, so the silhouette is the same in every accent; a test checks it.
- **Leaders (for 4.5):**
  - **API:** `NpcFactory.ChooseLeader(seed, race, index)` gives `ChooseParts(seed, race, LeaderIndexBase + index)` marked `IsLeader` (`NpcSpec.AsLeader()`). `Spawn` draws the race's leader design.
    - Leaders have their own index space, `LeaderIndexBase` = 0x800000. It sits above every population's base and inside `NpcSeed`'s 24-bit index field (review patch). So a leader is never the ordinary NPC at the same index: its salt, lines and wander differ.
    - `DisplayName` is "<race> leader".
    - `Signature()` appends `+leader` only for a leader. No population uses leaders yet, so no fixture field changes.
    - Debug: Shift+F3 spawns one leader of each race; F3 is unchanged.
  - **Look:** every leader has a gold crown, royal purple `#68386c` trim with a gold hem, and never varies. No ordinary NPC wears the purple; a test checks it. Per race:
    - **Town:** a crown, a white beard and hair, and a staff.
    - **Finches:** a crown, an Elizabethan ruff and a mantle.
    - **Chemistry folk:** a crown on the cap, a robe, and a staff topped with a mint flask.
    - **Physics folk:** a gold circlet in a wild white mane, and a robe.
    - **Shape people:** a crown of three triangles, a purple triangle body and a ruler staff.
    - **Ground dinosaurs:** a crown, a cape and a bone club.
    - **Avian dinosaurs:** a crown among long crest plumes, and a mantle.
- **The parts system stays, behind a switch:**
  - `NpcFactory.PartArt` is a static bool, off by default. When it is on, `Spawn` draws the old three stacked part sprites (`NpcPartArt`, unchanged); when it is off, `Spawn` draws one "Body" sprite.
  - `ChooseParts`, `RacePartSets`, `NpcSpec.With` and part-driven hints, roles and movement are unchanged and always used.
  - The `NpcPartArt` tests (Readability, Math shape people, InWorldText finches) test that art directly and still pass. The PlayMode parts tests turn the switch on and reset it in `TearDown`.
- **Newton's apple** lands on `NpcFactory.HeadTop(spec)`, the design's drawn top: `NpcDesignArt.DrawnRows` / 16 of 2 units. The townsperson's top row holds outline, so that is 16/16 (review patch). With the switch on, it is the parts' head.
- **Tests (none removed):**
  - EditMode `ReadabilityTests`:
    - `NpcDesigns_AreOutlinedSixteenPixelFigures_InThePlayersStyle`: the player's outline colour; bulk and height near the player's; feet on the bottom edge; a closed, two-pixel outline; point filter.
    - `NpcDesigns_EachRaceHasItsOwnSilhouette_AndItsLeaderStandsOut`: at least 12 silhouette pixels differ between any two designs and between a design and its leader; the leader has a gold crown and purple; accents vary the tint only; the accent is seeded.
  - EditMode `InWorldTextTests.NpcDesigns_AndTheirLeaders_StandApartFromEveryRegionFloor`: every design, accent and leader against both shades of all six floors, by the same rule as the floor test: contrast 1.5 or ΔE 20.
  - PlayMode `NpcComponentTests`:
    - New: `Spawn_DrawsItsRacesDesign_AsOneSprite_AndALeaderItsLeaderDesign`.
    - Updated: the 4.1 size test now checks the design: 1.33x a first-screen tile, the player's size, standing on its feet, collider unchanged. The hostile-tint test reads the "Body" renderer.
    - Kept with the switch on: the old parts size test (`SwappableParts_...WhenSwitchedOn`) and `Spawn_StacksThreePartSprites_..._WhenTheSwappablePartsAreSwitchedOn`.
- **Review patches (pass 1, all low):** see the Review Triage Log.
  - The leader index space, above.
  - `NpcFactory.PartArt` is reset on play start (`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, like `GamePause`), since domain reload is off.
  - The InWorldText scan now includes leader speaker names (4 per race per seed, so both dinosaur kinds appear).
  - The race-to-design mapping is pinned race by race in EditMode, with a colour cue per design, so a swap fails.
  - `HeadTop` is the actual drawn top.
  - New EditMode test: `NpcTests.Leaders_HaveTheirOwnIndexSpace_SoALeaderIsNeverTheOrdinaryNpcAtItsIndex`. It checks the salt differs, the signature equals the reserved index's, the leader is deterministic, and fewer than half the leaders say the same lines as the NPC at their index.
- **Determinism:** NPC visuals are in no signature, so it was not re-recorded. The fixture is unchanged and `DeterminismTests` passes.
- **Docs:** `ART-MAP.md` gains an NPC designs section, and the NPC parts entry notes the switch. The `NpcPartArt` and `ArtCatalog` summaries note it too.
- **Screenshots** are in the scratchpad's `snaps43/`, seed 7. They came from a temporary snapshot test, since deleted.
  - `lineup-<Region>.png`, one per floor: row 1 has one NPC of each design (Townsfolk, Finch, Chemistry, Physics, Shape, ground dinosaur, avian dinosaur), row 2 their leaders and row 3 the other two accents. The player is bottom right for comparison.
  - `lineup-zoom.png`: a close-up of the Town lineup.
  - `town-start.png` and `face-<Region>-11.png`: NPCs in place.

## Verification

**Result (2026-10-07, `vpart.sh` steps in the foreground as `v43a`):**
- EditMode 266/266 (3 new tests).
- PlayMode batches 25/22/26/27/38/24/1, all passing. Batch 4 has the 2 new NPC tests.
- Skill checks Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing.
- Fixture unchanged.
- No builder or scene regeneration was needed: NPCs are spawned at runtime and the art catalog is unchanged.
- Not committed.

**Result after the review patches (2026-10-07, `vpart.sh` steps in the foreground as `v43b`):**
- EditMode 267/267 (1 more: the leader index space).
- PlayMode batches 25/22/26/27/38/24/1, all passing.
- Skill checks Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing.
- Fixture unchanged.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 0, low 5, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| low | patch | `ChooseLeader(seed, race, i)` equals the ordinary NPC at index i (same salt, lines, wander) | Patched: leaders draw from their own index space (`LeaderIndexBase` = 0x800000). EditMode test `Leaders_HaveTheirOwnIndexSpace_...`. |
| low | patch | `NpcFactory.PartArt` survives between plays with domain reload off | Patched: reset by `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, like `GamePause`. |
| low | patch | Leader display names are missing from the InWorldText speaker scan | Patched: `ChooseLeader` names for every race, 4 per seed, seeds 1–50. |
| low | patch | The race-to-design mapping is not pinned, so a swap could pass | Patched: explicit `LookOf` asserts for every race and dinosaur kind, plus a colour cue per design. |
| low | patch | `HeadTop` used 15/16, but the drawn top is 16/16 where row 0 holds outline | Patched: `HeadTop(spec)` uses the design's actual top row (`NpcDesignArt.DrawnRows`). |
