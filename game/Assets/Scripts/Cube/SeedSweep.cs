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
    /// item, the Chemistry plan is checked too (ChemistryPlan.FindProblems: stage coverage, timed stages on the
    /// home face, an off-home gate, and dispenser-first: the isotope dispenser is reachable without passing any
    /// Chemistry-stage gate). With a Physics item, the Physics plan is checked too (PhysicsPlan.FindProblems: the
    /// Mass Mitt opens a timed door on another face, and, with the module library, every timed door's switch timing,
    /// its own boulders on its screen with enough of them reachable, no boulder starting in a time field or on
    /// another face's interactable, and the Physics trial fitting). When the catalog knows hidden-item slots
    /// (IHiddenSlotCatalog), the hidden meta-currency placement is checked too (HiddenCurrencyPlacement.FindProblems:
    /// 4-6 items in distinct hidden-item slots on built faces, each reachable from Town, outside every gated alcove
    /// and off the exit lanes).
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

            /// <summary>The hidden meta-currency placement that was checked; null when it was not checked.</summary>
            public HiddenCurrencyPlacement HiddenCurrency;

            /// <summary>Hidden meta-currency problems; null when it was not checked.</summary>
            public List<string> HiddenProblems;

            public bool CoreChecked => CoreProblems != null;
            public bool ItemsChecked => ItemProblems != null;
            public bool HiddenChecked => HiddenProblems != null;

            public bool Passed => Unreachable.Count == 0 && (CoreProblems == null || CoreProblems.Count == 0) &&
                                  (ItemProblems == null || ItemProblems.Count == 0) &&
                                  (HiddenProblems == null || HiddenProblems.Count == 0);
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
        public static List<string> FindItemProblems(CubeModel model, ItemPlacement placement, IEnumerable<Theme> items)
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

            // Collection simulation. Modules keep all four exits open and gates only close alcoves, so a
            // pickup is reachable when its screen is reachable and its guarding gate (if any) is open.
            HashSet<ScreenAddress> reachable = FindReachable(model);
            var held = new HashSet<Theme>();
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (PickupPlacement p in placement.Pickups)
                {
                    if (held.Contains(p.Item) || !reachable.Contains(p.Screen)) continue;
                    if (p.IsGuarded)
                    {
                        if (!placement.TryGetGate(p.Screen, p.GuardSlot, out GatePlacement guard)) continue;
                        if (!held.Contains(guard.Item)) continue;
                    }
                    held.Add(p.Item);
                    progress = true;
                }
            }
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
            IReadOnlyList<BeakKind> biologyVariantKinds = null)
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
                    if (catalog is IGateSlotCatalog gateCatalog)
                    {
                        List<Theme> items = ItemPlacement.RunItems(layout, itemThemes);
                        ItemPlacement placement = ItemPlacement.ForRun(model, layout, gateCatalog, itemThemes, variantCounts);
                        result.Placement = placement;
                        result.ItemProblems = FindItemProblems(model, placement, items);
                        foreach (string problem in BiologyPlan.FindProblems(model, placement, biologyVariantKinds))
                            result.ItemProblems.Add("biology: " + problem);
                        Func<ScreenAddress, ScreenModule> moduleAt = catalog is ModuleLibrary library ? ModuleLookup(layout, library) : null;
                        foreach (string problem in ChemistryPlan.FindProblems(model, placement, moduleAt))
                            result.ItemProblems.Add("chemistry: " + problem);
                        foreach (string problem in PhysicsPlan.FindProblems(model, placement, moduleAt, biologyVariantKinds))
                            result.ItemProblems.Add("physics: " + problem);
                    }
                    if (catalog is IHiddenSlotCatalog hiddenCatalog)
                    {
                        HiddenCurrencyPlacement hidden = HiddenCurrencyPlacement.Generate(model, layout, hiddenCatalog);
                        result.HiddenCurrency = hidden;
                        Func<ScreenAddress, ScreenModule> hiddenModuleAt = catalog is ModuleLibrary hiddenLibrary ? ModuleLookup(layout, hiddenLibrary) : null;
                        result.HiddenProblems = HiddenCurrencyPlacement.FindProblems(model, layout, hidden, hiddenCatalog, hiddenModuleAt);
                    }
                }
                results.Add(result);
            }
            return results;
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
            bool coreChecked = false, itemsChecked = false, hiddenChecked = false;
            foreach (SeedResult r in results)
            {
                total++;
                coreChecked |= r.CoreChecked;
                itemsChecked |= r.ItemsChecked;
                hiddenChecked |= r.HiddenChecked;
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
            }
            string what = coreChecked
                ? "fully reachable with a reachable core entrance on every built face"
                : "fully reachable";
            if (itemsChecked)
                what += ", every item collectable with no softlocks, optional gates guarding nothing required, " +
                        "the isotope dispenser before any Chemistry-stage gate, the Chemistry trial fitting, " +
                        "every timed door with enough reachable boulders on its screen, the Physics trial fitting";
            if (hiddenChecked)
                what += ", hidden meta currency reachable and never behind a gate";
            return $"Seed sweep: {total - failed}/{total} seeds {what}, {failed} failing.\n{failures}";
        }
    }
}
