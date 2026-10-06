- source_plan: `_bmad-output/initiative-video-game/epic-playable-cube/story-screen-modules-and-the-town-exit-shuffle-plan.md`
  summary: Town has only 4 hand-built modules, so at N=3 (epic-varied-runs' 3×3 expansion) Town cells reuse modules via index wrap.
  evidence: ModuleLibrary.TownModule uses index % townModules.Count, and ModuleLibraryBuilder makes 4 Town designs. Add 9 Town modules (or a per-N set) when faces go to 3×3.
- source_plan: `_bmad-output/initiative-video-game/epic-playable-cube/story-biology-face-darwin-vertical-slice-plan.md`
  summary: Optional (non-rolled) beak gates close empty alcoves; story 1.12 should place some hidden meta-currency (or other optional reward) behind them so they read as optional content.
  evidence: ItemPlacement step 4 puts optional gates in otherwise empty slots and nothing is placed behind them; the other beak is never obtainable in that run.
