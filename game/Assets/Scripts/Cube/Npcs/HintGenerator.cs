using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>How much hints give away. The first run uses Sparse: fewer hints that name only a face or theme.</summary>
    public enum HintDensity { Sparse = 0, Full = 1 }

    /// <summary>Something that makes NPC lines at a hint density (the town, the face populations, the debug spawns); the RunLoop sets it each run.</summary>
    public interface IHintDensityTarget
    {
        HintDensity HintDensity { get; set; }
    }

    /// <summary>
    /// The run facts hints are made from: the model, plus the science layout and item placement when known.
    /// Layout and placement are pure functions of the seed, so they can be computed before the reveal
    /// (Of(world) does that) and still match what the reveal builds.
    /// </summary>
    public sealed class RunFacts
    {
        public CubeModel Model { get; }

        /// <summary>The science layout, or null (then core-entrance hints fall back to face hints).</summary>
        public CubeLayout Layout { get; }

        /// <summary>The item placement, or null (then item and gate hints fall back to face hints).</summary>
        public ItemPlacement Placement { get; }

        /// <summary>
        /// The look of every required gate as the face plans build it (GateLooks.Planned), or null: then a gate hint names
        /// its item's run-wide look (HintGenerator.GateLook).
        /// </summary>
        public IReadOnlyDictionary<(ScreenAddress Screen, int Slot), string> GateLooks { get; }

        public RunFacts(CubeModel model, CubeLayout layout = null, ItemPlacement placement = null,
            IReadOnlyDictionary<(ScreenAddress Screen, int Slot), string> gateLooks = null)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Layout = layout;
            Placement = placement;
            GateLooks = gateLooks;
        }

        /// <summary>The facts of a run from its pure inputs, with every gate's planned look (what RunFacts.Of builds).</summary>
        public static RunFacts Planned(CubeModel model, CubeLayout layout, ItemPlacement placement, ModuleLibrary library,
            ItemCatalog catalog, int physicsMaxRequired = PhysicsPlan.DefaultMaxRequired) =>
            new RunFacts(model, layout, placement,
                Cube.GateLooks.Planned(model, layout, placement, library, catalog, physicsMaxRequired));

        /// <summary>
        /// The facts of a world's run. Before the reveal the layout and placement are generated the same way
        /// the reveal will (same seed, same library, same catalog), so hints are already true in town.
        /// </summary>
        public static RunFacts Of(CubeWorld world)
        {
            if (world == null || world.Model == null) throw new ArgumentException("World has no model", nameof(world));
            CubeLayout layout = world.Layout;
            ItemPlacement placement = world.ItemPlacement;
            if (layout == null && world.Library != null) layout = CubeLayout.Generate(world.Model, world.Library);
            if (placement == null && layout != null && world.Library != null && world.ItemCatalog != null)
                placement = ItemPlacement.ForRun(world.Model, layout, world.Library, world.ItemCatalog.Themes(),
                    world.ItemCatalog.VariantCounts());
            // The planned gate looks are worked out once per run (the plans are pure functions of the seed) and kept on the world.
            if (world.PlannedGateLooks == null && placement != null)
            {
                PhysicsFace physics = world.GetComponent<PhysicsFace>();
                world.PlannedGateLooks = Cube.GateLooks.Planned(world.Model, layout, placement, world.Library, world.ItemCatalog,
                    physics != null ? physics.MaxRequired : PhysicsPlan.DefaultMaxRequired);
            }
            return new RunFacts(world.Model, layout, placement, world.PlannedGateLooks);
        }
    }

    /// <summary>One hint: its kind, the facts it states (for checking), and the in-world line.</summary>
    public readonly struct Hint
    {
        /// <summary>The kind actually produced (a fallback when the run lacks the data for the head's kind).</summary>
        public readonly HintKind Kind;

        /// <summary>True when the hint names a screen (item, gate, core) or the town edge (face): full density.</summary>
        public readonly bool Precise;

        /// <summary>The face the hint is about.</summary>
        public readonly FaceId Face;

        /// <summary>The theme of that face (what the line names).</summary>
        public readonly Theme FaceTheme;

        /// <summary>For item and gate hints, the item's home theme; otherwise the face theme.</summary>
        public readonly Theme Item;

        /// <summary>The screen named, when Precise and the kind has a screen.</summary>
        public readonly ScreenAddress Screen;

        public readonly string Text;

        /// <summary>For a gate hint (and an item hint behind a gate), the gate's look as named in the text; else null.</summary>
        public readonly string GateLook;

        public Hint(HintKind kind, bool precise, FaceId face, Theme faceTheme, Theme item, ScreenAddress screen, string text,
            string gateLook = null)
        {
            GateLook = gateLook;
            Kind = kind;
            Precise = precise;
            Face = face;
            FaceTheme = faceTheme;
            Item = item;
            Screen = screen;
            Text = text;
        }

        public override string ToString() => Text;
    }

    /// <summary>
    /// Pure hint generation from the real run data, so every hint is true. A head's HintKind decides what
    /// is talked about; the NPC's salt picks which subject. Full density gives up to FullCount hints naming
    /// the screen (or the town edge); Sparse gives one hint naming only the face's theme.
    /// </summary>
    public static class HintGenerator
    {
        /// <summary>Density of the first run.</summary>
        public const HintDensity FirstRunDensity = HintDensity.Sparse;

        /// <summary>Density for a run, from the saved run count (runs ended so far): sparse on the first run (0), full afterwards.</summary>
        public static HintDensity DensityFor(int runCount) => runCount <= 0 ? FirstRunDensity : HintDensity.Full;

        public const int FullCount = 2;
        public const int SparseCount = 1;

        private static readonly string[] FullOpeners = { "They say", "Word around the ruins is", "A little bird told me" };
        private static readonly string[] SparseOpeners = { "Rumour has it", "Somebody once mumbled that", "I dreamt that" };

        public static List<Hint> Generate(RunFacts facts, HintKind kind, uint salt, HintDensity density)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            HintKind actual = Available(facts, kind) ? kind : HintKind.FaceTheme;
            int subjects = SubjectCount(facts, actual);
            int wanted = density == HintDensity.Full ? FullCount : SparseCount;
            int count = Math.Min(wanted, subjects);
            var hints = new List<Hint>(count);
            int start = (int)(salt % (uint)subjects);
            for (int i = 0; i < count; i++)
                hints.Add(Make(facts, actual, (start + i) % subjects, density, (int)((salt / 7u + (uint)i) % 3u)));
            return hints;
        }

        /// <summary>True when the run has the data for this kind of hint.</summary>
        public static bool Available(RunFacts facts, HintKind kind) => SubjectCount(facts, kind) > 0;

        private static int SubjectCount(RunFacts facts, HintKind kind)
        {
            switch (kind)
            {
                case HintKind.ItemLocation: return facts.Placement != null ? facts.Placement.Pickups.Count : 0;
                case HintKind.GateLocation: return facts.Placement != null ? facts.Placement.Gates.Count : 0;
                case HintKind.CoreEntrance: return facts.Layout != null ? CoreFaces(facts).Count : 0;
                default: return CubeModel.ScienceThemes.Count;
            }
        }

        private static List<FaceLayout> CoreFaces(RunFacts facts)
        {
            var list = new List<FaceLayout>();
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (facts.Layout.TryGetFace((FaceId)f, out FaceLayout fl)) list.Add(fl);
            return list;
        }

        private static Hint Make(RunFacts facts, HintKind kind, int subject, HintDensity density, int opener)
        {
            CubeModel model = facts.Model;
            bool full = density == HintDensity.Full;
            string open = full ? FullOpeners[opener] : SparseOpeners[opener];
            switch (kind)
            {
                case HintKind.ItemLocation:
                {
                    PickupPlacement p = facts.Placement.Pickups[subject];
                    Theme faceTheme = model.ThemeOf(p.Screen.Face);
                    string text, look = null;
                    if (full)
                    {
                        string guard = "";
                        if (p.IsGuarded && facts.Placement.TryGetGate(p.Screen, p.GuardSlot, out GatePlacement g))
                        {
                            look = LookOf(facts, g);
                            guard = $", tucked behind {look}";
                        }
                        text = $"{open} the {ItemNickname(p.Item)} lies on the {ScreenPhrase(p.Screen, model.FaceSize)} screen of {ThemeName(faceTheme)}{guard}.";
                    }
                    else
                    {
                        text = $"{open} the {ItemNickname(p.Item)} is somewhere in {ThemeName(faceTheme)}.";
                    }
                    return new Hint(kind, full, p.Screen.Face, faceTheme, p.Item, full ? p.Screen : default, text, look);
                }
                case HintKind.GateLocation:
                {
                    GatePlacement g = facts.Placement.Gates[subject];
                    Theme faceTheme = model.ThemeOf(g.Screen.Face);
                    string look = LookOf(facts, g);
                    string text = full
                        ? $"{open} {look} blocks a nook on the {ScreenPhrase(g.Screen, model.FaceSize)} screen of {ThemeName(faceTheme)}."
                        : $"{open} there's {look} out in {ThemeName(faceTheme)}. Rude of it.";
                    return new Hint(kind, full, g.Screen.Face, faceTheme, g.Item, full ? g.Screen : default, text, look);
                }
                case HintKind.CoreEntrance:
                {
                    FaceLayout fl = CoreFaces(facts)[subject];
                    var screen = new ScreenAddress(fl.Face, fl.CoreCell);
                    string text = full
                        ? $"{open} a door down into the core hides on the {ScreenPhrase(screen, model.FaceSize)} screen of {ThemeName(fl.Theme)}."
                        : $"{open} a way into the core is somewhere in {ThemeName(fl.Theme)}.";
                    return new Hint(kind, full, fl.Face, fl.Theme, fl.Theme, full ? screen : default, text);
                }
                default:
                {
                    Theme theme = CubeModel.ScienceThemes[subject];
                    FaceId face = model.FaceOf(theme);
                    bool sealedFace = model.IsSealed(face);
                    string where;
                    if (TryTownEdge(face, out Facing edge))
                        where = full
                            ? $"walk off town's {EdgeName(edge)} side and you're in {ThemeName(theme)}"
                            : $"{ThemeName(theme)} is right next door to town";
                    else
                        where = full
                            ? $"{ThemeName(theme)} is on the far side of the cube, straight through from town"
                            : $"{ThemeName(theme)} is a long way from town";
                    string tail = sealedFace ? ", though it's sealed up tight for now." : ".";
                    return new Hint(HintKind.FaceTheme, full, face, theme, theme, default, $"{open} {where}{tail}");
                }
            }
        }

        /// <summary>True if the face borders Town, with the Town edge that leads to it.</summary>
        public static bool TryTownEdge(FaceId face, out Facing edge)
        {
            for (int e = 0; e < 4; e++)
            {
                if (CubeModel.NeighborFace(CubeModel.StartFace, (Facing)e) != face) continue;
                edge = (Facing)e;
                return true;
            }
            edge = default;
            return false;
        }

        public static string EdgeName(Facing edge)
        {
            switch (edge)
            {
                case Facing.North: return "north";
                case Facing.East: return "east";
                case Facing.South: return "south";
                default: return "west";
            }
        }

        /// <summary>A screen in compass words within its face, e.g. "north-east"; "centre" for the middle.</summary>
        public static string ScreenPhrase(ScreenAddress screen, int faceSize)
        {
            string ns = faceSize > 1 ? (screen.Cell.y == 0 ? "south" : screen.Cell.y == faceSize - 1 ? "north" : "") : "";
            string ew = faceSize > 1 ? (screen.Cell.x == 0 ? "west" : screen.Cell.x == faceSize - 1 ? "east" : "") : "";
            if (ns.Length > 0 && ew.Length > 0) return ns + "-" + ew;
            if (ns.Length > 0) return "middle " + ns;
            if (ew.Length > 0) return "middle " + ew;
            return "centre";
        }

        /// <summary>The Math face's in-world placeholder region name (InWorldText.Bridgelands).</summary>
        public const string MathRegionName = InWorldText.Bridgelands;

        /// <summary>The Earth and Atmosphere face's in-world placeholder region name (InWorldText.Wildlands).</summary>
        public const string EarthRegionName = InWorldText.Wildlands;

        /// <summary>
        /// A face's name in hints, as it reads mid-sentence: its in-world region name (InWorldText.Region), never the
        /// science's name: "the Greenwood*", "the Ember Vale*", "the Weightlands*", "Bridgelands*", "Wildlands*", Town.
        /// </summary>
        public static string ThemeName(Theme theme) => InWorldText.Region(theme);

        /// <summary>
        /// How the gates of a theme's item look in this run, as a noun phrase ("a dark doorway"): named by their look, never
        /// by the science. The run's rolled variant (the same ItemVariants roll the catalog makes) picks the look, so the
        /// phrase is true for every gate of that item; a theme whose gates come in two looks names both.
        /// </summary>
        public static string GateLook(int seed, Theme item)
        {
            switch (item)
            {
                case Theme.Biology:
                    return BiologyBeaks.Roll(seed) == BeakKind.Thin ? "a bramble or a button door" : "a mossy rock or a clay pot";
                case Theme.Chemistry:
                    switch (ChemistryIsotope.VariantOrder[ItemVariants.Roll(seed, Theme.Chemistry, ChemistryIsotope.VariantCount)])
                    {
                        case IsotopeStage.Glow: return "a dark doorway";
                        case IsotopeStage.Unstable: return "a cracked wall";
                        default: return "a heavy plated door";
                    }
                case Theme.Physics: return "a swinging door";
                case Theme.Math: return "a gap";
                case Theme.EarthAtmosphere:
                    switch (StormcallerItem.VariantOrder[ItemVariants.Roll(seed, Theme.EarthAtmosphere, StormcallerItem.VariantCount)])
                    {
                        case Phenomenon.Quake: return "a row of rock pillars";
                        case Phenomenon.Rain: return "a wall of fire";
                        case Phenomenon.Snow: return "a pool of water";
                        default: return "a pile of debris";
                    }
                default: return "a locked gate";
            }
        }

        /// <summary>
        /// The look a hint names for one gate: the look the face plan builds it with (RunFacts.GateLooks, a plain barred
        /// door where the face falls back), or, without planned looks, its item's run-wide look (GateLook).
        /// </summary>
        public static string LookOf(RunFacts facts, GatePlacement gate) =>
            facts.GateLooks != null && facts.GateLooks.TryGetValue((gate.Screen, gate.Slot), out string look)
                ? look
                : GateLook(facts.Model.Seed, gate.Item);

        /// <summary>Every gate look a hint can name (for the text checks).</summary>
        public static IEnumerable<string> AllGateLooks()
        {
            foreach (string look in GateLooks.All()) yield return look;
            yield return "a bramble or a button door";
            yield return "a mossy rock or a clay pot";
            yield return "a dark doorway";
            yield return "a cracked wall";
            yield return "a heavy plated door";
            yield return "a swinging door";
            yield return "a gap";
            yield return "a row of rock pillars";
            yield return "a wall of fire";
            yield return "a pool of water";
            yield return "a pile of debris";
            yield return "a locked gate";
        }

        /// <summary>What townsfolk call a face's item.</summary>
        public static string ItemNickname(Theme theme)
        {
            switch (theme)
            {
                case Theme.Biology: return "beak-thing";
                case Theme.Chemistry: return "changing stone";
                case Theme.Physics: return "heavy grabbing mitt";
                case Theme.Math: return "gap-crossing plank";
                case Theme.EarthAtmosphere: return "storm-calling stick";
                default: return "town trinket";
            }
        }
    }
}

namespace Game.Cube
{
    /// <summary>
    /// What each required gate of a run looks like, decided the way the faces build them: the face plans are made from
    /// the same pure inputs as on the reveal (BiologyFace, ChemistryFace, PhysicsFace, MathFace and EarthFace BeginReveal),
    /// and a gate gets its themed look exactly when that face's CreateGate would build its themed gate; otherwise (no plan,
    /// no spec, no door layout, a mismatched stage or wand) the world builds a plain gate (iron bars) and the look is Plain.
    /// Keep in step with the faces' CreateGate conditions.
    /// </summary>
    public static class GateLooks
    {
        public const string Plain = "a door of iron bars";
        public const string Rock = "a mossy rock";
        public const string Pot = "a clay pot";
        public const string ButtonDoor = "a button door";
        public const string Bramble = "a bramble";
        public const string DarkDoorway = "a dark doorway";
        public const string CrackedWall = "a cracked wall";
        public const string PlatedDoor = "a heavy plated door";
        public const string SwingingDoor = "a swinging door";
        public const string Gap = "a gap";
        public const string Pillars = "a row of rock pillars";
        public const string FireWall = "a wall of fire";
        public const string Pool = "a pool of water";
        public const string Debris = "a pile of debris";

        public static IEnumerable<string> All() => new[]
        {
            Plain, Rock, Pot, ButtonDoor, Bramble, DarkDoorway, CrackedWall, PlatedDoor, SwingingDoor, Gap, Pillars,
            FireWall, Pool, Debris,
        };

        public static string Of(IsotopeStage stage) =>
            stage == IsotopeStage.Glow ? DarkDoorway : stage == IsotopeStage.Unstable ? CrackedWall : PlatedDoor;

        public static string Of(Phenomenon kind)
        {
            switch (kind)
            {
                case Phenomenon.Quake: return Pillars;
                case Phenomenon.Rain: return FireWall;
                case Phenomenon.Snow: return Pool;
                default: return Debris;
            }
        }

        /// <summary>The look of every required gate of a run, keyed by screen and slot (empty without a placement or catalog).</summary>
        public static Dictionary<(ScreenAddress Screen, int Slot), string> Planned(CubeModel model, CubeLayout layout,
            ItemPlacement placement, ModuleLibrary library, ItemCatalog catalog, int physicsMaxRequired = PhysicsPlan.DefaultMaxRequired)
        {
            var looks = new Dictionary<(ScreenAddress Screen, int Slot), string>();
            if (model == null || placement == null || catalog == null) return looks;
            int seed = model.Seed;
            Func<ScreenAddress, ScreenModule> moduleAt = layout != null && library != null ? SeedSweep.ModuleLookup(layout, library) : null;
            List<BeakKind> beakKinds = BiologyBeaks.VariantKinds(catalog);
            List<IsotopeStage> stageKinds = ChemistryIsotope.VariantKinds(catalog);
            List<Phenomenon> wandKinds = StormcallerItem.VariantKinds(catalog);

            // The plans exactly as the faces make them at the reveal, and each gate's look as the face builds it (the plans
            // own both the build conditions and the look, so the hints cannot drift from the built gates).
            BiologyPlan biology = BiologyPlan.ForRun(model, placement, catalog.ItemFor(Theme.Biology, seed), beakKinds);
            ChemistryPlan chemistry = ChemistryPlan.ForRun(model, placement, catalog.ItemFor(Theme.Chemistry, seed), stageKinds);
            PhysicsPlan physics = PhysicsPlan.ForRun(model, placement, catalog.ItemFor(Theme.Physics, seed), moduleAt, beakKinds,
                physicsMaxRequired, stageKinds);
            MathPlan math = MathPlan.ForRun(model, placement, catalog.ItemFor(Theme.Math, seed), moduleAt, beakKinds, stageKinds);
            EarthPlan earth = EarthPlan.ForRun(model, placement, catalog.ItemFor(Theme.EarthAtmosphere, seed), moduleAt, wandKinds,
                beakKinds, stageKinds);

            foreach (GatePlacement g in placement.Gates)
            {
                // The item CubeWorld builds the gate for (its theme's rolled item).
                ItemDefinition item = catalog.ItemFor(g.Item, seed);
                string look = null;
                switch (g.Item)
                {
                    case Theme.Biology: look = biology?.GateLook(g.Screen, g.Slot, false); break;
                    case Theme.Chemistry: look = chemistry?.GateLook(g.Screen, g.Slot, false, item); break;
                    case Theme.Physics: look = physics?.GateLook(g.Screen, g.Slot, false); break;
                    case Theme.Math: look = math?.GateLook(g.Screen, g.Slot, false); break;
                    case Theme.EarthAtmosphere: look = earth?.GateLook(g.Screen, g.Slot, false, item); break;
                }
                looks[(g.Screen, g.Slot)] = look ?? Plain;
            }
            return looks;
        }
    }
}
