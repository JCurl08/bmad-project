using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>How much hints give away. The first run uses Sparse: fewer hints that name only a face or theme.</summary>
    public enum HintDensity { Sparse = 0, Full = 1 }

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

        public RunFacts(CubeModel model, CubeLayout layout = null, ItemPlacement placement = null)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Layout = layout;
            Placement = placement;
        }

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
            return new RunFacts(world.Model, layout, placement);
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

        public Hint(HintKind kind, bool precise, FaceId face, Theme faceTheme, Theme item, ScreenAddress screen, string text)
        {
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
                    string text;
                    if (full)
                    {
                        string guard = "";
                        if (p.IsGuarded && facts.Placement.TryGetGate(p.Screen, p.GuardSlot, out GatePlacement g))
                            guard = $", tucked behind a {ThemeName(g.Item)} gate";
                        text = $"{open} the {ItemNickname(p.Item)} lies on the {ScreenPhrase(p.Screen, model.FaceSize)} screen of the {ThemeName(faceTheme)} face{guard}.";
                    }
                    else
                    {
                        text = $"{open} the {ItemNickname(p.Item)} is somewhere on the {ThemeName(faceTheme)} face.";
                    }
                    return new Hint(kind, full, p.Screen.Face, faceTheme, p.Item, full ? p.Screen : default, text);
                }
                case HintKind.GateLocation:
                {
                    GatePlacement g = facts.Placement.Gates[subject];
                    Theme faceTheme = model.ThemeOf(g.Screen.Face);
                    string text = full
                        ? $"{open} a {ThemeName(g.Item)} gate blocks a nook on the {ScreenPhrase(g.Screen, model.FaceSize)} screen of the {ThemeName(faceTheme)} face."
                        : $"{open} there's a {ThemeName(g.Item)} gate out on {ThemeName(faceTheme)}. Rude of it.";
                    return new Hint(kind, full, g.Screen.Face, faceTheme, g.Item, full ? g.Screen : default, text);
                }
                case HintKind.CoreEntrance:
                {
                    FaceLayout fl = CoreFaces(facts)[subject];
                    var screen = new ScreenAddress(fl.Face, fl.CoreCell);
                    string text = full
                        ? $"{open} a door down into the core hides on the {ScreenPhrase(screen, model.FaceSize)} screen of the {ThemeName(fl.Theme)} face."
                        : $"{open} a way into the core is somewhere on {ThemeName(fl.Theme)}.";
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
                            ? $"walk off town's {EdgeName(edge)} edge and you're in {ThemeName(theme)}"
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

        public static string ThemeName(Theme theme)
        {
            switch (theme)
            {
                case Theme.EarthAtmosphere: return "Earth & Atmosphere";
                default: return theme.ToString();
            }
        }

        /// <summary>What townsfolk call a face's item.</summary>
        public static string ItemNickname(Theme theme)
        {
            switch (theme)
            {
                case Theme.Biology: return "beak-thing";
                case Theme.Chemistry: return "glowy decay-thing";
                case Theme.Physics: return "heavy time-rock";
                case Theme.Math: return "pointy proof-thing";
                case Theme.EarthAtmosphere: return "weather wand";
                default: return "town trinket";
            }
        }
    }
}
