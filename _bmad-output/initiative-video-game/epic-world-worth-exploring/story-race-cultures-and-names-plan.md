---
title: 'Race cultures and names'
type: 'feature'
ticket: '4'
created: '2026-10-07'
status: 'built'
baseline_revision: '318a4da'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/faces-and-races.md'
  - '{project-root}/_bmad-output/initiative-video-game/spec-entropy-cube/lore-and-tone.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user said: "Give all the characters more meaningful names and I would like to give each race a separate culture and personality that is reflected by their maner of speech. Make the math people more logical, less friendly but to the point and still helpful in logical reasoning, chemical people more reactive emotionally (like a chemical reaction), the finches almost Shakespearean and proper, the physicists more philosophical and abstract, the earth science people I'm not sure." For Earth, the user later chose "rugged outdoorsy".

**Approach:** Give each race a voice, and rewrite all of its player-visible lines in that voice:
- dialogue, rumours and greetings
- `HintGenerator` output, including openers and connectors
- population lines, patron and leader lines
- trial and NPC errands

The voices:

| Race | Voice |
|---|---|
| Math shape people | Logical, terse, a little cool, but genuinely helpful. They reason in if/then steps. |
| Chemistry folk | Emotionally reactive: they flare up, fizz, calm down, and bond quickly. |
| Biology finches | Near-Shakespearean and proper ("Prithee, traveller…"), but still readable for pre-teens. |
| Physics folk | Philosophical and abstract; they muse on time, weight and motion. |
| Earth dinosaurs | Rugged and outdoorsy: practical, weather-wise, a bit gruff. The avian ones are breezier. |
| Townsfolk | A friendly, campy mix. |

**Names:**
- Every character gets a meaningful, unstarred name that fits its culture.
- That covers named NPCs, patrons, leaders (one per race, for story 4.5), the greeter and the Demon.
- Generic NPCs draw from a seeded per-race name pool.

</frozen-after-approval>

## Boundaries & Constraints

- **Subtle science:**
  - No scientist names and no explicit science vocabulary. `InWorldText` stays the single source for banned words.
  - Characters never know they are an analogy.
- **Names and `*`:** region and item names keep `*` until the naming brainstorm; character names are unstarred (lore-and-tone rule).
- **Hint truth:** hints keep their meaning. The hint truth tests and the gate-look tests keep passing.
- **Length:** lines stay short (the dialogue-length tests from 1.15).
- **Determinism:** seeded names may use a new RNG stream (do not perturb existing streams). Re-record only if a signature includes text, with a note.

## Tasks & Acceptance

- [x] A voice style guide in `lore-and-tone.md`-adjacent docs: a new companion `voices-and-names.md` beside the epic, plus a pointer in `faces-and-races.md`.
- [x] Name pools per race; named leaders, patrons and the greeter.
- [x] Rewrite all lines per race; hints use per-race openers and connectors.
- [x] Tests:
  - a text scan (no stars on character names, banned words absent)
  - per-race voice markers (e.g. finch lines use archaic forms, Math lines contain no exclamation marks)
  - hint truth and length
- [x] Full verify.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
- **Voice guide:** `voices-and-names.md` sits beside the epic; `faces-and-races.md` points to it under "Cultures and voices". It covers the voices, the tested markers, every name and the fixed names that replace the 1.22 placeholders.
- **Voices** (`Voice` enum in `Npcs/NpcNames.cs`): Town, Finch, Mushroom, Alien, Shape, Ground and Sky.
  - *Assumption:* the dinosaurs get two voices by kind. Terrestrial dinosaurs speak Ground (rugged); avian ones speak Sky (breezier).
  - An NPC speaks its race's voice wherever it stands, the Town's visitors included. Townsfolk, the greeter and the apple sage speak Town.
- **Dialogue** (`Npcs/Dialogue.cs`):
  - `Flavours` is now keyed by voice. Each voice has its own role lines (merchant, quest-giver, smith, gossip, 3 each), wares, metal, errand, rumours and greetings. `DinoGreetings` and the shared `RoleLines` are gone.
  - Rewritten in voice: every patron table, the beak, stone and stone-lore lines, the mitt, island, gap-crosser, wand, neutral-wand and hazard lines.
  - New `LeaderLines`, one per race. A leader's conversation is its leader line, then its hint, in its voice. 4.5 adds the quests.
  - A rumour after ".", "?" or "!" now starts with a capital (`Fill`).
  - `Dialogue.MaxLineLength` is 200, three lines of the box. *Assumption:* no 1.15 length test existed, so this limit is new and tested. The old mitt line (about 250 characters) was shortened.
  - Kept: the facts the tests read ("thin" and "thick", "swing", "dark doorway", "pressure plate", "stones" and "come back", "switch", the mitt name, "boulder", "slower", "longer", "doorway" and "round", the wand words, and wind, snow, rain, crevice, calm).
  - *Assumption:* trials have no player-visible NPC text. The errands are the voices' `Errand` fields.
- **Hints** (`HintGenerator.VoiceTable`): each voice has 3 full and 3 sparse openers, a guard connector, a vague-gate remark, a sealed tail and a sentence end. `HintGenerator.For(spec, ...)` speaks in the NPC's voice; the old `Generate(facts, kind, salt, density)` keeps the Town voice.
  - Only the wording around the facts changes. The region, the "<where> screen", the nickname, the gate look, "<edge> side", "far side", "next door", "long way", "sealed" and "core" read the same in every voice.
  - The adjacent-face full hint now says "<region> lies just off town's <edge> side" in every voice. It used to say "walk off town's <edge> side and you're in <region>", which read badly after most openers.
- **Names** (`Npcs/NpcNames.cs`):
  - `NpcSpec.Name` is never in `Signature()`. `DisplayName` is "<Name> the <kind>", e.g. "Pipwick the finch merchant" or "Queen Wrennifer the finch leader".
  - Generic NPCs draw from their voice's pool: 24 Town names, 20 per finch, mushroom, alien and shape pool, 16 per dinosaur kind. Each pool is shuffled per (seed, voice) on the new stream `RngStreams.NpcNames` = 22, with an offset of 7 per population and the person's ordinal in it (numerals past the pool, see the review patches). So a population never repeats a name, and no other stream is touched.
  - Earth dinosaurs are named by their slot within their own kind (`EarthPopulation.DinoSpec`).
  - *Assumption (names, my choice):*
    - Leaders: Mayor Muddleton, Queen Wrennifer, Duchess Flarella, Grand Muser Yondrix, Chancellor Quadrilla, Chief Rockjaw.
    - Patrons: Grandsire Beakwell (was Old Fledgling\*), Mother Glimmercap (Glowmother\*), Old Whenever (Timekeeper\*), Madam Spanwell (Bridgekeeper\*), Granny Trowel (Old Digger\*), Auntie Cloudeye (Sky-Watcher\*).
    - Others: Grandpa Brittlecap (Marcel\*), Sage Conkwell (apple-bonked sage\*), the greeter Hattie Hullo, and the boss Primsworth the Demon (Demon\*).
    - The pools are listed in `voices-and-names.md`.
  - **The Demon:** "Demon" is now its unstarred title, with `InWorldText.DemonFull` = "Primsworth the Demon".
    - The Demon text changed: the defeat message, the victory title, "Bonk Primsworth!", "Primsworth dethroned" and the upgrade shop's "Primsworth calls that balanced".
    - The greeter's welcome now starts "Hullo, hullo! I'm Hattie."
- **InWorldText:** `Placeholders()` keeps only the regions and the currency. Character names count as chosen, as whole names only (tightened by the review patches). `ChosenNames` is unchanged (Town, Partition).
- **Tests:**
  - New `VoicesAndNamesTests` (5 tests, 8 after the review patches):
    - Every name has no star, no banned word and no repeats. Every NPC of seeds 1–50 is named from the pools or the fixed names, and a population never repeats a name. Leaders, the greeter and the sage are fixed.
    - The Demon and the patrons go by name.
    - Voice markers on every sampled line over seeds 1–50, from populations, Town, leaders, every role and the tables:
      - finch: an archaic form;
      - shape: no "!", plus if/then/therefore/thus/hence/given;
      - mushroom: "!" or a swing word;
      - alien: "?", "..." or a musing word;
      - dinosaurs: outdoorsy words.
      Each voice also words the same hint differently.
    - The voice rules catch lines out of voice.
    - Every line, and every hint in every voice, fits in 200 characters.
  - Extended: the shared scan (hints in all 7 voices, leader conversations), `Hints_NameRegionsAndGateLooks_NeverTheThemes` (all voices), and the NpcTests hint-truth tests (sparse and full in every voice, the fallback in every voice).
  - Updated: the per-face star checks (Chemistry, Earth, Math) use the shared `InWorldText.UnstarredNames`, and the patron assertions use the new names. The hint comparisons in Biology, Chemistry, Physics, Math, Earth, Readability and the Town, Biology, Chemistry and Physics scene tests use `HintGenerator.For(spec, ...)`. `RngStreamsTests` records NpcNames = 22. The TownTests speaker check now reads "the townsperson".
- **Review patches (pass 1, quick lens; see the Review Triage Log):**
  - **Hint endings:** the alien hints lose ", or so it seems."; the musing lives in the opener. `HintVoice.Questions` marks question openers ("I wonder... could it be that"), and `EndFor(opener)` ends their hints with "?"; every other hint ends with its voice's "." or "!".
  - **Names swapped:** Hailey becomes Cumulina (sky), Sumner becomes Cornelia (shape), Orbix becomes Quizzle (alien).
  - **Mitt line:** "Ponder the timed doors. Pile boulders by the door with the Heavy Mitt\* (Interact or swing): more weight, slower time, longer open. Then step on or hit the switch, and run. Why rush, I wonder?" (under 200 characters).
  - **Gap-crosser line:** "...swing the Gap-Crosser\* at it. It lays a crossing over that gap. Therefore, one swing, one gap."
  - **Unique names past the pool:** `NpcNames.For(seed, race, variant, offset, ordinal)` takes entry (offset + ordinal) mod pool size, and from the second round a numeral ("Bessie II"). Distinct ordinals in a population therefore always give distinct names. A 5x5 Town outgrows the Town pool and uses numerals (tested at N = 2, 3 and 5).
  - **Earth fallback:** a dinosaur whose 16 draws never gave its kind is named at ordinal `EarthPopulation.FallbackOrdinal` (100) + i, past every regular ordinal of its new kind. *Assumption:* it then carries a numeral ("Flint VII"); this is very rare.
  - **Chief Rockjaw** is always terrestrial: `NpcSpec.Variant` returns Terrestrial for the dinosaur leader, so his look, Ground voice and label ("Chief Rockjaw the terrestrial dinosaur leader") follow. His parts and Signature are unchanged.
  - **Greeter label:** "Hattie Hullo the greeter" (`NpcSpec.KindOverride`, set by `Named(name, kind)`). The welcome now says "I'm Hattie Hullo."
  - **Stricter star scan:** `InWorldText.UnstarredNames` skips a capitalised word only where a whole character name stands (`NpcNames.Phrases()`, longest first, plus a following numeral II–XX). A lone "Queen", "Old" or "Wrennifer" is flagged again. The all-capitals exemption is gone: only the listed shouts (RAWR, SKRAW, CLANG, BOOM, VICTORY, MAXED) pass, and other emphasis moved to `*stars*` ("I'm *so* happy", "How *dare* it!").
  - **Stricter voice tests:** markers are whole words (outdoor ones too). Character names, item names and nicknames, and region names are stripped first, case-sensitively, so "Cliff" never counts but "a cliff" does. The finch list drops "mine" and "pray". Mushroom lines need an emotional word, not just "!". Alien lines need a musing word, not just "?". Lines were reworded to keep every voice ("...Sob. I care so much.", "Or does the nap finish me, I wonder?").
  - **New tests:**
    - `EveryVoicesHints_EndWithOneFittingMark_ForEveryOpenerAndKind`: every voice, every opener, every kind and density, sealed faces included. One fitting end mark, "?" only after a question opener, no ", or so it seems".
    - `NamesStayUnique_WithinEveryPopulation_EvenOnAFiveByFiveFace_WhereThePoolsRunOut`, including the fallback ordinals.
    - `TheGreeter_AndTheDinosaurLeader_HaveTheirOwnLabels`.
    - More rule cases in `TheRule_...` and `TheVoiceRules_...`.
- **Determinism:** `NpcNames` is not added to the fixture's first-draws line, so the fixture is unchanged. Names are not in any signature.

## Verification

**Result (2026-10-07, `vpart.sh` steps in the foreground as `v44` and `v44b`):**
- EditMode 272/272 (5 new).
- PlayMode batches 25/22/26/27/38/24/1, all passing.
  - Batch 5 first failed one Town scene test that still compared hints in the Town voice. It passed after the test switched to `HintGenerator.For` (v44b).
  - Batches 0–4 are from v44: after them only test files changed.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing.
- Fixture unchanged.
- No builder or scene regeneration was needed: all the text is runtime.
- Not committed.

**Result after the review patches (2026-10-07, `vpart.sh` steps in the foreground as `v44c`):**
- EditMode 275/275 (3 more).
- PlayMode batches 25/22/26/27/38/24/1, all passing.
- Skill checks: Victory in 78.6 s, 90.5 s and 105.5 s (unchanged).
- `IslandWalkTests`: 0 problems.
- Sweep 50/50, 0 failing.
- Fixture unchanged.
- Not committed.

## Review Triage Log

Pass 1 (quick lens), triaged by the coordinator: high 0, medium 1, low 9, all patched.

| Verdict | Route | Finding | Outcome |
|---|---|---|---|
| medium | patch | Alien hints end ", or so it seems." even on precise hints, and "could it be that... ." is a question ending in "." | Patched: the musing is in the opener only, and question openers end with "?" (`HintVoice.Questions`, `EndFor`). New test over every voice, opener, kind and density for terminal punctuation. |
| low | patch | Names Hailey, Sumner and Orbix | Patched: Cumulina, Cornelia and Quizzle. |
| low | patch | The mitt tutorial lost its clear steps | Patched: pile boulders by the door, step on or hit the switch, run; in voice, under 200 characters. |
| low | patch | "One gap at a time, given its size." is unclear | Patched: "It lays a crossing over that gap. Therefore, one swing, one gap." |
| low | patch | `NpcNames.For` repeats names once a population outgrows its pool | Patched: an ordinal per population, with numerals from the second round. Tested at N = 2, 3 and 5. |
| low | patch | An Earth dinosaur on the kind fallback can share a name | Patched: fallback ordinal 100 + i, with a test. |
| low | patch | Chief Rockjaw can be avian | Patched: the dinosaur leader is always terrestrial (look, voice, label), with a test. |
| low | patch | The voice-marker tests are loose (prefix matches, names count, "mine" and "pray", a bare "!" or "?") | Patched: whole words; names, items and regions stripped; "mine" and "pray" dropped; Mushroom needs an emotional word, Alien a musing word. Lines reworded to fit. |
| low | patch | The star scan skips any word of any name, and every all-capitals word | Patched: only whole name phrases (with a numeral) are chosen, and only listed shouts; emphasis moved to *stars*. |
| low | patch | The greeter's label read "the townsperson ..." | Patched: "Hattie Hullo the greeter". |
