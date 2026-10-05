# Town Draft and Friendship

## Per-run draft (CAP-14, Should)
The Town face has one NPC of each race. Which faces exist in the run is fixed when the player enters town. The shuffle is rolled when the player leaves town.

| Talk order | Status for the run |
|---|---|
| First | **Ally** |
| Middle | Neutral |
| Last | **Enemy**: every member of the race attacks on sight |
| Skipped | **Enemy** |

**Ally bonuses:**
- The race's face item gets an upgrade or extra ability for the run.
- The race's NPCs give more helpful hints.
- Some otherwise inaccessible areas unlock.
- The race gives useful items, such as healing items.

Choosing an ally also works as a difficulty selector. Skipping several races is a self-chosen hard mode.

**Tone hook:** the last race talked to holds a campy grudge ("Oh, NOW you have time for me?").

## Persistent friendship score (CAP-18, Could)
| Event | Friendship |
|---|---|
| Talked to first | +2 |
| Talked to in the middle | +1 |
| Talked to last | +0 |
| Solve a race's problem while it is your enemy | +4 |
| Solve any other race's problem | +1 |

- **Threshold 1:** the race no longer turns hostile when talked to last, and solving its problems gives +1 instead of +4.
- **Late-game unlock:** the player can deliberately skip a race that is past threshold 1, making it an enemy again so they can earn the +4 bonus.
- **Threshold 2:** the race is always an ally and gives ally bonuses every run, whatever the talk order.
- Threshold values are not set yet and will be tuned by playtesting.

Design intent: helping your enemy is the fastest way to win them over. Over many runs, every race becomes friendly and the world un-partitions.
