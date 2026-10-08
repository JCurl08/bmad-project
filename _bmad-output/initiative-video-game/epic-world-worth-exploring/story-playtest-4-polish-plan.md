---
title: 'Playtest 4 polish'
type: 'feature'
ticket: '18'
created: '2026-10-08'
status: 'draft'
baseline_revision: ''
route: 'full'
route_source: 'auto'
risk: 'medium'
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

**Problem:** Playtest of 4.13, 2026-10-08. The user's notes:
1. "town fragement - make a side quest"
2. "players can't move during dialog and pause other players". The user confirmed this means: freeze the player and pause the other NPCs and enemies while a conversation is open.
4. "white face should be more white than grey"
5. "the farm looking people are kind of confusing and out of place". The user chose to replace them with a mix of all races.
6. "avian dinosaur is more like a penguin, if you can find a better sprite then please change it"
7. "The shape person is cute, maybe make them look less human like but I like the humpty dumpty style"
8. "Add a quest tracker. Each time someone gives you a quest add it to the log."
9. "Many NPCs have the same quest, maybe we can remove some of the duplicates."

**Approach:**
- **Dialogue freeze.** While a conversation is open:
  - the player can't move, swing or use items;
  - every other NPC and enemy pauses, including Demon shots and particles in the arena, and hazards;
  - wind, ice and the timers (Physics timed doors) hold too.
- **Quest log.** Every quest a character gives is added to a log you can view, either on the pause map or with a key such as Q. It holds:
  - leader quests, with their fragment progress;
  - the Town side quest;
  - NPC errands.

  Entries update as progress is made and are marked done.
- **Fewer duplicate quests.** Each errand or quest line is given by at most one NPC per face. Other NPCs give hints or flavour instead.
- **Town side quest (lost-and-found).**
  - The Mayor says Town's fragment went missing.
  - A seeded trail of 2–3 townsfolk across Town's screens each sends you to the next.
  - One of them wants a small favour, such as something from a Town chest or a word with another townsperson.
  - The last one hands over the fragment. The Mayor then mends Town.
- **Town look.**
  - The Town floor reads white, not grey, while keeping contrast.
  - Town's residents become a mix of all races.
  - Only the Mayor and the greeter (Hattie Hullo) keep a unique town look, without the farm or straw-hat style.
- **Avian dinosaur.** Redesign it to read as a feathered raptor or bird-dino, not a penguin. Use a Kenney tile if one fits, otherwise draw it in code.
- **Shape person.** Make it less human and more Humpty Dumpty: an egg or round geometric body with little limbs, keeping its charm.

</frozen-after-approval>

## Boundaries & Constraints

- The seed sweep stays at 50/50. Re-record determinism, with a note.
- The subtle-science and voice rules still hold (InWorldText, voices-and-names.md).
- The readability tests stay green.
- PlayMode tests run in class batches.
- Nothing from later Epic 4 stories, except the hooks they need.

## Tasks & Acceptance

- [ ] Dialogue freeze (player, NPCs, enemies, hazards, timers, arena) with tests.
- [ ] Quest log (data, HUD or map view, entries from every quest-giver, progress, done) with tests.
- [ ] Deduplicate quest-giver lines per face. Add a test that no two NPCs on a face give the same quest.
- [ ] Town lost-and-found side quest on its own RNG stream. The sweep proves it is completable on seeds 1–50. Add a PlayMode test that walks it.
- [ ] Whiter Town floor; townsfolk become a mixed-race cast; new Mayor and greeter looks.
- [ ] Avian dinosaur and shape person redesigns, with the readability tests updated.
- [ ] Re-record determinism with a note. Run a full verify. Take screenshots.

## Implementation Notes

- Self-approved under the overnight instruction (2026-10-07).
