using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Reachability check: breadth-first search from the Town start screen over CubeModel.TryStep.
    /// Every screen on an unsealed face must be reachable. Given a module catalog it also lays out the
    /// science faces (CubeLayout) and checks that every built science face has exactly one core-entrance
    /// slot, on a reachable screen. When the catalog also knows gate slots (IGateSlotCatalog) it places
    /// the items (ItemPlacement) and simulates collecting them: starting with nothing, repeatedly collect
    /// every reachable pickup whose guarding gate (if any) is opened by an item already held; any item
    /// left uncollected is a softlock. Optional gates (non-rolled item variants, e.g. the other beak) must
    /// guard nothing required: no pickup may sit behind one, and none may share a slot with a required gate.
    /// With Biology beak variants, the Biology plan is checked too (BiologyPlan.FindProblems: the rolled beak
    /// has an off-home gate, a thin-beak run has its flower-vine bridge, the trial is on Biology). With a Chemistry
    /// stone (variant), the Chemistry plan is checked too (ChemistryPlan.FindProblems: required gates use the rolled
    /// stone, optional ones the other stones, on Chemistry, the rolled stone opens a gate on another face, the trial
    /// fits; the optional-gate rule above covers the other stones' gates too). With a Physics item, the Physics plan is checked too (PhysicsPlan.FindProblems: the
    /// Mass Mitt opens a timed door on another face, and, with the module library, every timed door's switch timing,
    /// its own boulders on its screen with enough of them reachable, no boulder starting in a time field or on
    /// another face's interactable, and the Physics trial fitting). With a Math item, the Math plan is checked too
    /// (MathPlan.FindProblems: the gap-crosser* opening a gap gate on another face, Math's island screens laid out with
    /// every doorway on its own island and the islands deliberately not all joined, every ordinary screen reachable without
    /// the gap-crosser* and every island with it, every join two-way so the player is never stranded, the trial island
    /// reachable, and every item collected doorway by doorway). The item collection here walks doorway by doorway too
    /// (DoorwayReach over Math's island screens): an open pickup on an island screen counts only where its island is
    /// reachable. With an Earth item, the Earth plan is checked too
    /// (EarthPlan.FindProblems: required gates use the rolled Stormcaller* phenomenon and it opens a gate on another face,
    /// optional ones the other phenomena, on Earth; the calm eye at the centre and no hazard on it or on a timed door's
    /// screen; every hazard within PlayerMover's bounds, so it only slows or deflects; every crevice off the lanes, clear of
    /// everything that stands on its screen and cutting nothing off; the trial fitting). When the catalog knows hidden-item slots
    /// (IHiddenSlotCatalog), the hidden meta-currency placement is checked too (HiddenCurrencyPlacement.FindProblems:
    /// 4-6 items in distinct hidden-item slots on built faces, each reachable from Town, outside every gated alcove
    /// and off the exit lanes). With both, the roadblock rewards are checked too (4.7, RewardPlacement.FindProblems: every
    /// gate, optional gate and hidden spot hides a reward, no item lies loose, every face has its fragment slots, key and
    /// boost, every non-town screen holds a roadblock or is an island screen, and every required reward is reachable).
    /// Shared by the tests and the editor menu.
    /// </summary>
    public static class SeedSweep
    {
        public const int DefaultFirstSeed = 1;
        public const int DefaultSeedCount = 50;

        public struct SeedResult
        {
            public int Seed;
            public List<ScreenAddress> Unreachable;

            /// <summary>Core-entrance problems (face and reason); null when no catalog was given.</summary>
            public List<string> CoreProblems;

            /// <summary>Item and gate problems (softlocks, gate counts); null when items were not checked.</summary>
            public List<string> ItemProblems;

            /// <summary>The item placement that was checked; null when items were not checked.</summary>
            public ItemPlacement Placement;

            /// <summary>Towns checked this seed (FindTownProblems: every laid-out face's, and the Town face's with its module).</summary>
            public int TownsChecked;

            /// <summary>The hidden meta-currency placement that was checked; null when it was not checked.</summary>
            public HiddenCurrencyPlacement HiddenCurrency;

            /// <summary>Hidden meta-currency problems; null when it was not checked.</summary>
            public List<string> HiddenProblems;

            /// <summary>The roadblock rewards that were checked (4.7); null when they were not checked.</summary>
            public RewardPlacement Rewards;

            /// <summary>Roadblock reward problems (RewardPlacement.FindProblems); null when they were not checked.</summary>
            public List<string> RewardProblems;

            /// <summary>The face-edge locks that were checked (4.9); null when they were not checked.</summary>
            public EdgeLocks Locks;

            /// <summary>The lock check (4.9: no spending order soft-locks, enough first edges); null when not checked.</summary>
            public LockReport LockReport;

            /// <summary>Lock problems (EdgeLocks.Check); null when they were not checked.</summary>
            public List<string> LockProblems;

            /// <summary>The ice screens that were checked (4.10); null when there was no layout.</summary>
            public IcePlan Ice;

            /// <summary>Ice screen problems (IcePlan.Problems: solvable from every doorway inside its tier's targets); null when not checked.</summary>
            public List<string> IceProblems;

            /// <summary>The wind screens that were checked (4.11); null when there was no layout.</summary>
            public WindPlan Wind;

            /// <summary>Wind screen problems (WindPlan.Problems: solvable from every doorway inside its tier's targets); null when not checked.</summary>
            public List<string> WindProblems;

            /// <summary>Math's island screens that were checked (4.12); null when there was no layout.</summary>
            public IslandPlan Islands;

            /// <summary>Island screen problems (IslandPlan.Problems: lit from every doorway inside its tier's targets); null when not checked.</summary>
            public List<string> IslandProblems;

            /// <summary>The boss lair that was checked (4.13); null without a catalog.</summary>
            public LairPlan Lair;

            /// <summary>Lair problems (LairPlan.FindProblems: one portal to the arena, on a reachable screen, every other into a lair room with its hint-givers); null when not checked.</summary>
            public List<string> LairProblems;

            /// <summary>Town's lost-and-found trail that was checked (4.18); null when not checked.</summary>
            public TownTrail TownTrail;

            /// <summary>
            /// Quest problems (4.18: Town's trail completable, TownTrail.FindProblems, and every town's one errand sound); null
            /// when not checked.
            /// </summary>
            public List<string> QuestProblems;

            public bool CoreChecked => CoreProblems != null;
            public bool QuestsChecked => QuestProblems != null;
            public bool ItemsChecked => ItemProblems != null;
            public bool HiddenChecked => HiddenProblems != null;
            public bool RewardsChecked => RewardProblems != null;
            public bool LocksChecked => LockProblems != null;
            public bool IceChecked => IceProblems != null;
            public bool WindChecked => WindProblems != null;
            public bool IslandsChecked => IslandProblems != null;
            public bool LairChecked => LairProblems != null;

            public bool Passed => Unreachable.Count == 0 && (CoreProblems == null || CoreProblems.Count == 0) &&
                                  (ItemProblems == null || ItemProblems.Count == 0) &&
                                  (HiddenProblems == null || HiddenProblems.Count == 0) &&
                                  (RewardProblems == null || RewardProblems.Count == 0) &&
                                  (LockProblems == null || LockProblems.Count == 0) &&
                                  (IceProblems == null || IceProblems.Count == 0) &&
                                  (WindProblems == null || WindProblems.Count == 0) &&
                                  (IslandProblems == null || IslandProblems.Count == 0) &&
                                  (LairProblems == null || LairProblems.Count == 0) &&
                                  (QuestProblems == null || QuestProblems.Count == 0);
        }

        /// <summary>
        /// Quest problems of a run (4.18; empty when all is well): Town's trail (TownTrail.FindProblems against the Town
        /// population's planned spots: 2-3 people on different screens off the square, a helper, every line in the rules),
        /// its people all on screens reachable from the start, and every town's errand (Town's and each laid-out face's): a
        /// giver and a different target, both among the town's people, with lines in the rules.
        /// </summary>
        public static List<string> FindQuestProblems(CubeModel model, ModuleLibrary library, out TownTrail trail)
        {
            var problems = new List<string>();
            int n = model.FaceSize;
            trail = TownTrail.For(model.Seed, n);
            List<NpcSpec> specs = TownPlan.Specs(model.Seed, n);
            List<TownSpot> spots = TownPlan.Spots(model.Seed, n, specs.Count, cell => TownPlan.BlockedRects(library.TownModule(cell, n)));
            foreach (string p in TownTrail.FindProblems(model.Seed, n, trail, spots)) problems.Add("trail: " + p);
            if (trail != null)
            {
                HashSet<ScreenAddress> reachable = FindReachable(model);
                foreach (int index in new List<int>(trail.Steps) { trail.Helper })
                    if (index < spots.Count && spots[index].Found && !reachable.Contains(spots[index].Screen))
                        problems.Add($"trail: person {index} on unreachable {spots[index].Screen}");
            }
            Dictionary<FaceId, ErrandPlan> errands = Errands.ForRun(model);
            if (!errands.ContainsKey(CubeModel.StartFace)) problems.Add("errand: Town has none");
            foreach (KeyValuePair<FaceId, ErrandPlan> e in errands)
            {
                ErrandPlan plan = e.Value;
                if (plan.GiverIndex == plan.TargetIndex) problems.Add($"errand: {e.Key}'s giver is its target");
                if (plan.Giver.Role != NpcRole.QuestGiver && plan.Giver.KindOverride == null) problems.Add($"errand: {e.Key}'s giver is not labelled");
                foreach (string line in new[] { Dialogue.ErrandLine(plan.Giver, plan.Target.Name), QuestLines.DeliveredLine(plan.Target.Voice, plan.Giver.Name) })
                {
                    if (line.Length > Dialogue.MaxLineLength) problems.Add($"errand: too long ({line.Length}): {line}");
                    foreach (string p in InWorldText.Problems(line)) problems.Add($"errand: {p}: {line}");
                }
            }
            return problems;
        }

        /// <summary>Every screen reachable from the start screen.</summary>
        public static HashSet<ScreenAddress> FindReachable(CubeModel model)
        {
            var visited = new HashSet<ScreenAddress> { model.StartScreen };
            var queue = new Queue<ScreenAddress>();
            queue.Enqueue(model.StartScreen);
            while (queue.Count > 0)
            {
                ScreenAddress current = queue.Dequeue();
                for (int f = 0; f < 4; f++)
                {
                    if (model.TryStep(current, (Facing)f, out ScreenAddress next, out _) && visited.Add(next))
                        queue.Enqueue(next);
                }
            }
            return visited;
        }

        /// <summary>Screens on unsealed faces that cannot be reached from the start screen.</summary>
        public static List<ScreenAddress> FindUnreachable(CubeModel model)
        {
            HashSet<ScreenAddress> visited = FindReachable(model);
            var unreachable = new List<ScreenAddress>();
            foreach (ScreenAddress screen in model.AllScreens())
                if (!model.IsSealed(screen.Face) && !visited.Contains(screen))
                    unreachable.Add(screen);
            return unreachable;
        }

        /// <summary>
        /// Problems with the core entrances of a layout: every built science face needs exactly one
        /// active core-entrance slot on a reachable screen; Town and sealed faces must not be laid out.
        /// Empty when all is well.
        /// </summary>
        public static List<string> FindCoreProblems(CubeModel model, CubeLayout layout, IModuleCatalog catalog)
        {
            var problems = new List<string>();
            HashSet<ScreenAddress> reachable = FindReachable(model);
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                Theme theme = model.ThemeOf(face);
                bool laidOut = layout.TryGetFace(face, out FaceLayout fl);
                if (!CubeLayout.IsLaidOut(model, face))
                {
                    if (laidOut) problems.Add($"{face} ({theme}) should not be laid out");
                    continue;
                }
                if (!laidOut)
                {
                    problems.Add($"{face} ({theme}) has no layout");
                    continue;
                }

                // Count active core entrances over the whole face: the slot is active only on CoreCell.
                int active = 0;
                for (int y = 0; y < model.FaceSize; y++)
                {
                    for (int x = 0; x < model.FaceSize; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        int slots = catalog.CoreSlotCount(theme, fl.ModuleAt(cell));
                        if (cell == fl.CoreCell && fl.CoreSlot >= 0 && fl.CoreSlot < slots) active++;
                    }
                }
                if (active != 1)
                {
                    problems.Add($"{face} ({theme}) has {active} active core entrances");
                    continue;
                }
                var coreScreen = new ScreenAddress(face, fl.CoreCell);
                if (!reachable.Contains(coreScreen))
                    problems.Add($"{face} ({theme}) core entrance on unreachable {coreScreen}");
            }
            return problems;
        }

        /// <summary>
        /// Problems with an item placement. Every item must have at least one gate on its home face and at
        /// least one on another built face, gates must sit in distinct slots on laid-out faces, and each
        /// item needs exactly one pickup on its home face. Then collection is simulated from an empty
        /// inventory; every item left uncollected (behind its own gate, behind a cycle, or behind a gate
        /// whose item is itself stuck) is reported as a softlock naming the item. Empty when all is well.
        /// </summary>
        public static List<string> FindItemProblems(CubeModel model, ItemPlacement placement, IEnumerable<Theme> items,
            Func<ScreenAddress, IScreenRegions> islandAt = null)
        {
            var problems = new List<string>();
            var itemList = new List<Theme>(items);
            var builtFaces = new HashSet<FaceId>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(model, (FaceId)f)) builtFaces.Add((FaceId)f);

            var usedSlots = new HashSet<(ScreenAddress, int)>();
            foreach (GatePlacement gate in placement.Gates)
            {
                if (!builtFaces.Contains(gate.Screen.Face))
                    problems.Add($"{gate} is not on a built science face");
                if (!usedSlots.Add((gate.Screen, gate.Slot)))
                    problems.Add($"{gate} shares its slot with another gate");
            }
            problems.AddRange(FindOptionalGateProblems(model, placement, usedSlots, builtFaces));

            foreach (Theme item in itemList)
            {
                FaceId home = model.FaceOf(item);
                int onHome = 0, offHome = 0;
                foreach (GatePlacement gate in placement.Gates)
                {
                    if (gate.Item != item) continue;
                    if (gate.Screen.Face == home) onHome++;
                    else if (builtFaces.Contains(gate.Screen.Face)) offHome++;
                }
                if (onHome < 1) problems.Add($"{item} item has no gate on its home face {home}");
                if (offHome < 1 && builtFaces.Count > 1) problems.Add($"{item} item has no gate on another built face");

                int count = 0;
                foreach (PickupPlacement p in placement.Pickups)
                {
                    if (p.Item != item) continue;
                    count++;
                    if (p.Screen.Face != home) problems.Add($"{item} pickup is on {p.Screen.Face}, not its home face {home}");
                    if (p.IsGuarded && !placement.TryGetGate(p.Screen, p.GuardSlot, out _))
                        problems.Add($"{p}: no gate in that slot");
                }
                if (count != 1) problems.Add($"{item} item has {count} pickups");
            }

            // Collection simulation, doorway by doorway. Ordinary modules keep all four doorways joined and gates only close
            // alcoves, so there a pickup is reachable when its screen is and its guarding gate (if any) is open; on an island
            // screen (islandAt) only where its island is reachable, with or without the gap-crosser* (the Math item).
            HashSet<Theme> held = CollectableItems(model, placement, islandAt);
            foreach (Theme item in itemList)
            {
                if (held.Contains(item)) continue;
                string where;
                if (!placement.TryGetPickup(item, out PickupPlacement p))
                    where = "it has no pickup";
                else if (p.IsGuarded && placement.TryGetGate(p.Screen, p.GuardSlot, out GatePlacement guard))
                    where = $"its pickup at {p.Screen} is behind a {guard.Item} gate" + StuckCause(placement, item, guard.Item);
                else
                    where = $"its pickup at {p.Screen} cannot be reached";
                problems.Add($"softlock: {item} item can never be collected, {where}");
            }
            return problems;
        }

        /// <summary>
        /// The items a run can collect, starting with nothing: repeatedly every pickup whose place is reachable doorway by
        /// doorway (DoorwayReach over Math's island screens, islandAt; the gap-crosser* once held) and whose guarding gate,
        /// if any, is opened by an item already held. Shared by the item and the reward checks (RewardPlacement).
        /// </summary>
        public static HashSet<Theme> CollectableItems(CubeModel model, ItemPlacement placement, Func<ScreenAddress, IScreenRegions> islandAt = null)
        {
            var reach = new DoorwayReach(model, islandAt);
            var held = new HashSet<Theme>();
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (PickupPlacement p in placement.Pickups)
                {
                    if (held.Contains(p.Item) || !reach.CanReach(p.Screen, MathPlan.PickupLocal(p), held.Contains(Theme.Math))) continue;
                    if (p.IsGuarded)
                    {
                        if (!placement.TryGetGate(p.Screen, p.GuardSlot, out GatePlacement guard)) continue;
                        if (!held.Contains(guard.Item)) continue;
                    }
                    held.Add(p.Item);
                    progress = true;
                }
            }
            return held;
        }

        /// <summary>
        /// Problems with optional gates: each sits on its item's home face (a built face), in a slot no other
        /// gate uses, is never the run's rolled variant, and guards nothing required (no pickup behind it).
        /// </summary>
        public static List<string> FindOptionalGateProblems(CubeModel model, ItemPlacement placement)
        {
            var usedSlots = new HashSet<(ScreenAddress, int)>();
            foreach (GatePlacement gate in placement.Gates) usedSlots.Add((gate.Screen, gate.Slot));
            var builtFaces = new HashSet<FaceId>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(model, (FaceId)f)) builtFaces.Add((FaceId)f);
            return FindOptionalGateProblems(model, placement, usedSlots, builtFaces);
        }

        private static List<string> FindOptionalGateProblems(CubeModel model, ItemPlacement placement,
            HashSet<(ScreenAddress, int)> usedSlots, HashSet<FaceId> builtFaces)
        {
            var problems = new List<string>();
            foreach (OptionalGatePlacement gate in placement.OptionalGates)
            {
                if (!builtFaces.Contains(gate.Screen.Face))
                    problems.Add($"{gate} is not on a built science face");
                else if (gate.Screen.Face != model.FaceOf(gate.Item))
                    problems.Add($"{gate} is not on its item's home face");
                if (!usedSlots.Add((gate.Screen, gate.Slot)))
                    problems.Add($"{gate} shares its slot with another gate");
                if (gate.Variant == placement.RolledVariant(gate.Item))
                    problems.Add($"{gate} uses the run's rolled variant");
                foreach (PickupPlacement p in placement.Pickups)
                    if (p.IsGuarded && p.Screen == gate.Screen && p.GuardSlot == gate.Slot)
                        problems.Add($"{gate} guards required content: the {p.Item} pickup");
            }
            return problems;
        }

        /// <summary>
        /// Why a stuck item's guard gate never opens: it is the item's own gate, the guard chain leads
        /// back to the item (a genuine cycle), or the guard item is itself stuck for another reason.
        /// </summary>
        private static string StuckCause(ItemPlacement placement, Theme item, Theme guardItem)
        {
            if (guardItem == item) return " (its own gate)";
            Theme current = guardItem;
            for (int step = 0; step <= placement.Pickups.Count; step++)
            {
                if (!placement.TryGetPickup(current, out PickupPlacement p) || !p.IsGuarded ||
                    !placement.TryGetGate(p.Screen, p.GuardSlot, out GatePlacement next))
                    break;
                if (next.Item == item) return " that can never be opened (cycle)";
                current = next.Item;
            }
            return $" whose item ({guardItem}) is itself stuck";
        }

        /// <summary>
        /// Checks seeds firstSeed .. firstSeed + count - 1. With a catalog, also checks the core
        /// entrances of each seed's science layout, and (if it knows gate slots) the item placement the
        /// game would make: pass the item catalog's themes (ItemCatalog.Themes()) as itemThemes and its
        /// variant counts (ItemCatalog.VariantCounts()) so the swept placement is the played one
        /// (ItemPlacement.ForRun); null themes means every laid-out theme, null variants means none.
        /// </summary>
        public static List<SeedResult> Run(int firstSeed = DefaultFirstSeed, int count = DefaultSeedCount,
            int faceSize = CubeSettings.DefaultFaceSize, IModuleCatalog catalog = null,
            IEnumerable<Theme> itemThemes = null, IReadOnlyDictionary<Theme, int> variantCounts = null,
            IReadOnlyList<BeakKind> biologyVariantKinds = null, IReadOnlyList<IsotopeStage> chemistryVariantKinds = null,
            IReadOnlyList<Phenomenon> earthVariantKinds = null)
        {
            var results = new List<SeedResult>(count);
            for (int s = firstSeed; s < firstSeed + count; s++)
            {
                var model = new CubeModel(s, faceSize);
                var result = new SeedResult { Seed = s, Unreachable = FindUnreachable(model) };
                if (catalog != null)
                {
                    CubeLayout layout = CubeLayout.Generate(model, catalog);
                    result.CoreProblems = FindCoreProblems(model, layout, catalog);
                    // 4.13: one portal to the arena, the rest into the lair, each room with its hint-givers.
                    result.Lair = LairPlan.ForRun(model);
                    result.LairProblems = LairPlan.FindProblems(model, layout, result.Lair, FindReachable(model));
                    // 4.18: Town's lost-and-found trail is completable, and every town has one sound errand.
                    if (catalog is ModuleLibrary questLibrary && TownSquare.HasTown(faceSize))
                        result.QuestProblems = FindQuestProblems(model, questLibrary, out result.TownTrail);
                    MathPlan islands = null;
                    // 4.10: Earth's ice screens, solved from every doorway against their tier's targets.
                    IcePlan ice = IcePlan.Create(model, layout);
                    result.Ice = ice;
                    result.IceProblems = ice != null ? ice.Problems(model, catalog.IslandModuleIndex(Theme.EarthAtmosphere) >= 0) : new List<string>();
                    // 4.11: and its wind screens, the same way.
                    WindPlan wind = WindPlan.Create(model, layout);
                    result.Wind = wind;
                    result.WindProblems = wind != null ? wind.Problems(model, catalog.WindModuleIndex(Theme.EarthAtmosphere) >= 0) : new List<string>();
                    // 4.12: and Math's island screens, lit from every doorway against their tier's targets.
                    IslandPlan islandPlan = IslandPlan.Create(model, layout);
                    result.Islands = islandPlan;
                    result.IslandProblems = islandPlan != null ? islandPlan.Problems(model, catalog.IslandModuleIndex(Theme.Math) >= 0) : new List<string>();
                    List<PuzzleGoal> puzzles = PuzzleScreens.Goals(ice, wind, islandPlan);
                    Func<ScreenAddress, IScreenRegions> regionsAt = PuzzleScreens.RegionsAt(null, ice, wind, islandPlan);
                    if (catalog is IGateSlotCatalog gateCatalog)
                    {
                        List<Theme> items = ItemPlacement.RunItems(layout, itemThemes);
                        ItemPlacement placement = ItemPlacement.ForRun(model, layout, gateCatalog, itemThemes, variantCounts);
                        result.Placement = placement;
                        Func<ScreenAddress, ScreenModule> moduleAt = catalog is ModuleLibrary library ? ModuleLookup(layout, library) : null;
                        islands = moduleAt != null ? MathPlan.Create(model, placement, moduleAt) : null;
                        regionsAt = PuzzleScreens.RegionsAt(islands, ice, wind, islandPlan);
                        result.ItemProblems = FindItemProblems(model, placement, items, regionsAt);
                        foreach (string problem in BiologyPlan.FindProblems(model, placement, biologyVariantKinds))
                            result.ItemProblems.Add("biology: " + problem);
                        foreach (string problem in ChemistryPlan.FindProblems(model, placement, chemistryVariantKinds, moduleAt))
                            result.ItemProblems.Add("chemistry: " + problem);
                        foreach (string problem in PhysicsPlan.FindProblems(model, placement, moduleAt, biologyVariantKinds,
                            PhysicsPlan.DefaultMaxRequired, chemistryVariantKinds))
                            result.ItemProblems.Add("physics: " + problem);
                        foreach (string problem in MathPlan.FindProblems(model, placement, moduleAt, biologyVariantKinds, chemistryVariantKinds))
                            result.ItemProblems.Add("math: " + problem);
                        foreach (string problem in EarthPlan.FindProblems(model, placement, earthVariantKinds, moduleAt, biologyVariantKinds,
                            chemistryVariantKinds))
                            result.ItemProblems.Add("earth: " + problem);
                        ScreenModule townFaceCentre = catalog is ModuleLibrary townLibrary
                            ? townLibrary.TownModule(TownSquare.Cell(faceSize), faceSize) : null;
                        foreach (string problem in FindTownProblems(model, layout, catalog, placement, biologyVariantKinds,
                            chemistryVariantKinds, earthVariantKinds, moduleAt, townFaceCentre, out result.TownsChecked))
                            result.ItemProblems.Add("town: " + problem);
                    }
                    if (catalog is IHiddenSlotCatalog hiddenCatalog)
                    {
                        HiddenCurrencyPlacement hidden = HiddenCurrencyPlacement.Generate(model, layout, hiddenCatalog);
                        result.HiddenCurrency = hidden;
                        Func<ScreenAddress, ScreenModule> hiddenModuleAt = catalog is ModuleLibrary hiddenLibrary ? ModuleLookup(layout, hiddenLibrary) : null;
                        result.HiddenProblems = HiddenCurrencyPlacement.FindProblems(model, layout, hidden, hiddenCatalog, hiddenModuleAt);
                        foreach (HiddenCurrencySpot spot in hidden.Spots)
                            if (TownSquare.IsTown(spot.Screen, model.FaceSize)) result.HiddenProblems.Add($"{spot} is in a town");
                        if (result.Placement != null)
                        {
                            // 4.7: every roadblock hides a reward, required ones reachable, no empty screen.
                            result.Rewards = RewardPlacement.Generate(model, layout, result.Placement, hidden, hiddenCatalog, puzzles);
                            result.RewardProblems = RewardPlacement.FindProblems(model, layout, result.Placement, hidden, result.Rewards,
                                hiddenCatalog, regionsAt, puzzles);
                            // 4.9: the face-edge locks, over every order of spending the keys (the game's 3x3 faces only: the
                            // N = 2 tests keep the unlocked cube, as they keep no towns).
                            if (TownSquare.HasTown(faceSize))
                            {
                                var lockWorld = new LockWorld(model, layout, result.Placement, result.Rewards,
                                    regionsAt, puzzles);
                                result.Locks = EdgeLocks.Generate(lockWorld, out result.LockReport);
                                result.LockProblems = result.LockReport.Problems;
                            }
                        }
                    }
                }
                results.Add(result);
            }
            return results;
        }

        /// <summary>
        /// Problems with the towns of a run (4.5; empty when all is well): given townFaceCentre (the Town face's centre module),
        /// it is Town's slot-free town module with a sound layout; on every laid-out face with a town, the centre holds
        /// the theme's town module (when the catalog has one) and is neither the core screen nor an island screen; no gate,
        /// optional gate or pickup stands on a town screen; no face plan puts its trial (Biology, Chemistry, Physics, Math,
        /// Earth) or a flower there; and the Earth calm eye is the Earth town. Given the module prefabs, the town module has
        /// no slots and its town (TownLayout) is sound: buildings, a statue, a leader's stand and stands for every resident,
        /// all clear of the lanes, so its four doorways stay open.
        /// </summary>
        public static List<string> FindTownProblems(CubeModel model, CubeLayout layout, IModuleCatalog catalog, ItemPlacement placement,
            IReadOnlyList<BeakKind> biologyVariantKinds = null, IReadOnlyList<IsotopeStage> chemistryVariantKinds = null,
            IReadOnlyList<Phenomenon> earthVariantKinds = null, Func<ScreenAddress, ScreenModule> moduleAt = null,
            ScreenModule townFaceCentre = null) =>
            FindTownProblems(model, layout, catalog, placement, biologyVariantKinds, chemistryVariantKinds, earthVariantKinds, moduleAt,
                townFaceCentre, out _);

        /// <summary>FindTownProblems, also counting the towns it checked (each laid-out face's, plus the Town face's when given).</summary>
        public static List<string> FindTownProblems(CubeModel model, CubeLayout layout, IModuleCatalog catalog, ItemPlacement placement,
            IReadOnlyList<BeakKind> biologyVariantKinds, IReadOnlyList<IsotopeStage> chemistryVariantKinds,
            IReadOnlyList<Phenomenon> earthVariantKinds, Func<ScreenAddress, ScreenModule> moduleAt, ScreenModule townFaceCentre,
            out int townsChecked)
        {
            var problems = new List<string>();
            int n = model.FaceSize;
            townsChecked = 0;
            if (!TownSquare.HasTown(n)) return problems;
            // The Town face's centre (its start screen): Town's own town module, slot-free, with a sound layout.
            if (townFaceCentre != null)
            {
                townsChecked++;
                if (!townFaceCentre.IsTownScreen || townFaceCentre.Theme != Theme.Town)
                    problems.Add($"Town's centre module {townFaceCentre.name} is not Town's town");
                if (townFaceCentre.Gates.Length > 0 || townFaceCentre.HiddenItems.Length > 0 || townFaceCentre.CoreEntranceCount > 0)
                    problems.Add($"Town's town module {townFaceCentre.name} has slots");
                foreach (string p in TownLayout.For(Theme.Town).Problems()) problems.Add($"Town: {p}");
            }
            bool IsTown(ScreenAddress s) => TownSquare.IsTown(s, n);
            for (int f = 0; f < CubeSettings.FaceCount; f++)
            {
                var face = (FaceId)f;
                if (!layout.TryGetFace(face, out FaceLayout fl)) continue;
                townsChecked++;
                Vector2Int cell = TownSquare.Cell(n);
                int town = catalog != null ? catalog.TownModuleIndex(fl.Theme) : -1;
                if (town >= 0 && fl.ModuleAt(cell) != town) problems.Add($"{face} ({fl.Theme}): the centre is not its town");
                if (fl.CoreCell == cell) problems.Add($"{face} ({fl.Theme}): the core portal is in the town");
                if (fl.IsPuzzleCell(cell)) problems.Add($"{face} ({fl.Theme}): a puzzle screen is the town");
                ScreenModule module = moduleAt?.Invoke(new ScreenAddress(face, cell));
                if (module != null)
                {
                    if (!module.IsTownScreen) problems.Add($"{face}: the centre module {module.name} is not a town module");
                    if (module.Gates.Length > 0 || module.HiddenItems.Length > 0 || module.CoreEntranceCount > 0)
                        problems.Add($"{face}: the town module {module.name} has slots");
                    foreach (string p in TownLayout.For(fl.Theme).Problems()) problems.Add($"{face}: {p}");
                }
            }
            if (placement != null)
            {
                foreach (GatePlacement g in placement.Gates) if (IsTown(g.Screen)) problems.Add($"{g} is in a town");
                foreach (OptionalGatePlacement g in placement.OptionalGates) if (IsTown(g.Screen)) problems.Add($"{g} is in a town");
                foreach (PickupPlacement p in placement.Pickups) if (IsTown(p.Screen)) problems.Add($"{p} is in a town");
                BiologyPlan biology = BiologyPlan.Create(model, placement, biologyVariantKinds);
                if (biology != null)
                {
                    if (IsTown(biology.TrialScreen)) problems.Add($"the Biology trial is in the town {biology.TrialScreen}");
                    foreach (BeakGateSpec g in biology.Gates)
                        if (g.Kind == BeakGateKind.FlowerVine && IsTown(g.FlowerScreen)) problems.Add($"{g}: its flower is in a town");
                }
                ChemistryPlan chemistry = ChemistryPlan.Create(model, placement, chemistryVariantKinds);
                if (chemistry != null && IsTown(chemistry.TrialScreen)) problems.Add($"the Chemistry trial is in the town {chemistry.TrialScreen}");
                PhysicsPlan physics = PhysicsPlan.Create(model, placement, moduleAt, biologyVariantKinds, PhysicsPlan.DefaultMaxRequired,
                    chemistryVariantKinds);
                if (physics != null && IsTown(physics.TrialScreen)) problems.Add($"the Physics trial is in the town {physics.TrialScreen}");
                if (moduleAt != null)
                {
                    MathPlan math = MathPlan.Create(model, placement, moduleAt, biologyVariantKinds, chemistryVariantKinds);
                    if (math != null)
                    {
                        if (math.IslandScreens.Count > 0 && IsTown(math.TrialScreen)) problems.Add($"the Math trial is in the town {math.TrialScreen}");
                        foreach (IslandScreenSpec spec in math.IslandScreens)
                            if (IsTown(spec.Screen)) problems.Add($"{spec} is a town");
                    }
                }
                EarthPlan earth = EarthPlan.Create(model, placement, moduleAt, earthVariantKinds, biologyVariantKinds, chemistryVariantKinds);
                if (earth != null)
                {
                    if (IsTown(earth.TrialScreen)) problems.Add($"the Earth trial is in the town {earth.TrialScreen}");
                    if (!IsTown(earth.Eye)) problems.Add($"the calm eye {earth.Eye} is not the Earth town");
                }
            }
            return problems;
        }

        /// <summary>The module prefab of each screen of a layout (null off the laid-out faces).</summary>
        public static Func<ScreenAddress, ScreenModule> ModuleLookup(CubeLayout layout, ModuleLibrary library) =>
            screen => layout != null && library != null && layout.TryGetFace(screen.Face, out FaceLayout fl)
                ? library.Module(fl.Theme, fl.ModuleAt(screen.Cell))
                : null;

        /// <summary>Human-readable report naming every failing seed, its unreachable screens, core and item problems.</summary>
        public static string Describe(IEnumerable<SeedResult> results)
        {
            var failures = new StringBuilder();
            int total = 0, failed = 0;
            bool coreChecked = false, itemsChecked = false, hiddenChecked = false, rewardsChecked = false, locksChecked = false, iceChecked = false, windChecked = false, islandsChecked = false;
            bool lairChecked = false, questsChecked = false;
            foreach (SeedResult r in results)
            {
                total++;
                coreChecked |= r.CoreChecked;
                itemsChecked |= r.ItemsChecked;
                hiddenChecked |= r.HiddenChecked;
                rewardsChecked |= r.RewardsChecked;
                locksChecked |= r.LocksChecked;
                iceChecked |= r.IceChecked && r.Ice != null && r.Ice.Screens.Count > 0;
                windChecked |= r.WindChecked && r.Wind != null && r.Wind.Screens.Count > 0;
                islandsChecked |= r.IslandsChecked && r.Islands != null && r.Islands.Screens.Count > 0;
                lairChecked |= r.LairChecked;
                questsChecked |= r.QuestsChecked;
                if (r.Passed) continue;
                failed++;
                if (r.Unreachable.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": unreachable ")
                        .AppendLine(string.Join(", ", r.Unreachable));
                if (r.CoreProblems != null && r.CoreProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": core entrance ")
                        .AppendLine(string.Join("; ", r.CoreProblems));
                if (r.ItemProblems != null && r.ItemProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": items ")
                        .AppendLine(string.Join("; ", r.ItemProblems));
                if (r.HiddenProblems != null && r.HiddenProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": hidden currency ")
                        .AppendLine(string.Join("; ", r.HiddenProblems));
                if (r.RewardProblems != null && r.RewardProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": rewards ")
                        .AppendLine(string.Join("; ", r.RewardProblems));
                if (r.LockProblems != null && r.LockProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": locks ")
                        .AppendLine(string.Join("; ", r.LockProblems));
                if (r.IceProblems != null && r.IceProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": ice ")
                        .AppendLine(string.Join("; ", r.IceProblems));
                if (r.WindProblems != null && r.WindProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": wind ")
                        .AppendLine(string.Join("; ", r.WindProblems));
                if (r.IslandProblems != null && r.IslandProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": islands ")
                        .AppendLine(string.Join("; ", r.IslandProblems));
                if (r.LairProblems != null && r.LairProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": lair ")
                        .AppendLine(string.Join("; ", r.LairProblems));
                if (r.QuestProblems != null && r.QuestProblems.Count > 0)
                    failures.Append("Seed ").Append(r.Seed).Append(": quests ")
                        .AppendLine(string.Join("; ", r.QuestProblems));
            }
            string what = coreChecked
                ? "fully reachable with a reachable core entrance on every built face"
                : "fully reachable";
            if (itemsChecked)
                what += ", every item collectable with no softlocks, optional gates guarding nothing required, " +
                        "the rolled stone opening a gate on another face, the Chemistry trial fitting, " +
                        "every timed door with enough reachable boulders on its screen, the Physics trial fitting, " +
                        "the gap-crosser* opening a gate on another face, Math's island screens joining every doorway (one only over a " +
                        "broken span) yet never stranding the player, every item collected doorway by doorway, the Math trial island reachable, the rolled Stormcaller* opening a gate on another face, hazards that never " +
                        "block a way or trap the player, the calm eye at the centre, the Earth trial fitting, every face's centre its " +
                        "town with no gate, pickup, core portal, puzzle screen, trial, flower or hidden spot on it";
            if (hiddenChecked)
                what += ", hidden meta currency reachable and never behind a gate";
            if (rewardsChecked)
                what += ", every roadblock hiding a reward (no loose pickups, every required reward reachable, Town numbered 1 " +
                        "and the other faces 2-6, every face numbered k holding fragments 1..k, its key and boost) and no empty screen";
            if (iceChecked)
                what += ", every Earth ice screen solvable from each of its doorways inside its tier's targets with no hard traps";
            if (islandsChecked)
                what += ", every Math island screen lit from each of its doorways inside its tier's targets (light every plank: a clean " +
                        "start reachable, the tier's planks, parity and P_rand), its reward roadblock open only once solved";
            if (windChecked)
                what += ", every Earth wind screen solvable from each of its doorways inside every built target of its tier (fan switches not built) with no hard traps";
            if (locksChecked)
                what += ", every face-edge arch locked (a key or an item) with no order of spending keys soft-locking a screen, " +
                        "island, item, fragment, key or core portal, and at least two edges able to be opened first";
            if (lairChecked)
                what += ", exactly one core portal (the real one, on a reachable screen) leading to the arena and every other into a lair " +
                        "room whose prisoners give a fight hint, benefit hints and the real portal's region, all in the text rule";
            if (questsChecked)
                what += ", Town's lost-and-found trail completable (2-3 townsfolk on different reachable screens off the square, a favour " +
                        "helper, every line in the text rule) and one sound errand in every town";
            return $"Seed sweep: {total - failed}/{total} seeds {what}, {failed} failing.\n{failures}";
        }
    }
}
