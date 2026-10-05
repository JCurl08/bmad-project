# Scope Tiers

| Tier | Capabilities |
|---|---|
| Must | CAP-1 to CAP-13 |
| Should | CAP-14 to CAP-17, CAP-24 |
| Could | CAP-18 to CAP-23 |

## Checkpoints
| End of day | Gate |
|---|---|
| 2 | The same seed gives the same cube, and a new seed gives a different cube. Nothing else proceeds until this works. |
| 5 | The Biology vertical slice works end to end. If behind, drop to 2×2 faces (24 screens). |
| 8 | Four science faces are playable. If not, strip them to puzzle, item and gates only. |
| 10 | The boss works. If not, fall back to a simpler boss. |
| 11 | All Must items are done, and 3+ seeds play start to finish. Should work starts only once this passes. |
| 13 | Feature freeze. |
| 14 | Buffer: polish, seed sweep, balance. No new features. |

## Cut order (drop first, top to bottom)
1. Multiple demon victories (CAP-15)
2. Face-based demon phases (CAP-15). Keep the single phase.
3. Puzzle difficulty scaling (CAP-16)
4. Town draft (CAP-14)
5. Improved re-collected abilities (CAP-17)
6. Must-tier fallback: 3×3 faces become 2×2. Never cut puzzle types.
7. Must-tier fallback: the single-phase demon becomes a simpler boss.

If time is short, ship CAP-17 before CAP-19, because they overlap.
