- source_plan: `_bmad-output/initiative-video-game/epic-playable-cube/story-screen-modules-and-the-town-exit-shuffle-plan.md`
  summary: Town has only 4 hand-built modules, so at N=3 (epic-varied-runs' 3×3 expansion) Town cells reuse modules via index wrap.
  evidence: ModuleLibrary.TownModule uses index % townModules.Count, and ModuleLibraryBuilder makes 4 Town designs. Add 9 Town modules (or a per-N set) when faces go to 3×3.
