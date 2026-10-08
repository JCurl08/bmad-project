using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The hazards of the Earth face: wind bands push, freezing rain slows, crevices block. (4.10: the snow patches are gone;
    /// sliding is the ice screens' whole-screen puzzles now, IcePlan. 1 was Snow and is never reused.)
    /// </summary>
    public enum HazardKind { Wind = 0, FreezingRain = 2, Crevice = 3 }

    /// <summary>One Earth gate of the run: where it is, which phenomenon opens it, and whether it is an optional extra.</summary>
    public readonly struct EarthGateSpec : IEquatable<EarthGateSpec>
    {
        public readonly ScreenAddress Screen;
        public readonly int Slot;
        public readonly Phenomenon Kind;

        /// <summary>True when the gate is on the Earth face (its home).</summary>
        public readonly bool OnHome;

        /// <summary>True for the other phenomena's optional gates (guard nothing required).</summary>
        public readonly bool Optional;

        public EarthGateSpec(ScreenAddress screen, int slot, Phenomenon kind, bool onHome, bool optional)
        {
            Screen = screen;
            Slot = slot;
            Kind = kind;
            OnHome = onHome;
            Optional = optional;
        }

        public bool Equals(EarthGateSpec other) =>
            Screen == other.Screen && Slot == other.Slot && Kind == other.Kind && OnHome == other.OnHome && Optional == other.Optional;

        public override bool Equals(object obj) => obj is EarthGateSpec other && Equals(other);
        public override int GetHashCode() => ((Screen.GetHashCode() * 31 + Slot) * 31 + (int)Kind) * 31 + (Optional ? 1 : 0);

        public override string ToString() =>
            $"{(Optional ? "optional " : "")}{Kind} gate at {Screen} #{Slot}{(OnHome ? "" : " (off-home)")}";
    }

    /// <summary>
    /// One hazard on an Earth screen (local to the screen centre). Wind and freezing rain are areas the player walks
    /// through (they only change how the player moves, through PlayerMover's modifier hook); a crevice is a solid strip the
    /// player goes round. Strength: the wind's push (a fraction of the player's speed), the rain's speed multiplier;
    /// 0 for a crevice.
    /// </summary>
    public sealed class HazardSpec
    {
        public ScreenAddress Screen { get; }
        public HazardKind Kind { get; }

        /// <summary>The hazard's area (or the crevice's strip), local to the screen centre; empty for an unplaced crevice.</summary>
        public Rect Area { get; }

        /// <summary>The wind's direction (unit length); zero for the other kinds.</summary>
        public Vector2 Direction { get; }

        public float Strength { get; }

        public HazardSpec(ScreenAddress screen, HazardKind kind, Rect area, Vector2 direction, float strength)
        {
            Screen = screen;
            Kind = kind;
            Area = area;
            Direction = direction;
            Strength = strength;
        }

        /// <summary>True once the hazard has a place on its screen (a crevice needs the screen's module for that).</summary>
        public bool IsPlaced => Area.width > 0f && Area.height > 0f;

        public override string ToString()
        {
            string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            string where = IsPlaced ? $" [{F(Area.xMin)},{F(Area.yMin)} {F(Area.width)}x{F(Area.height)}]" : " (unplaced)";
            string dir = Kind == HazardKind.Wind ? $" toward ({F(Direction.x)},{F(Direction.y)})" : "";
            return $"{Kind} at {Screen}{where}{dir} strength {F(Strength)}";
        }
    }

    /// <summary>
    /// The Earth face of a run as pure data, from the run seed, the item placement and (for positions) the screen modules:
    /// the rolled phenomenon (the placement's rolled Earth variant, its kind read from the item ids); every Earth gate (all
    /// required ones the rolled phenomenon's, so it opens at least one gate on another face; the other phenomena's optional
    /// gates their own, guarding nothing required); the calm eye (the face's centre screen, no hazards: since 4.5 it hosts the
    /// Earth town, so no trial is ever there either); the hazards (one
    /// per other screen, the three kinds dealt round a seeded order; 4.10: never on an ice screen, Earth's whole-screen puzzle
    /// module, which IcePlan lays out); the difficulty (the hazard level, from the number of
    /// hazard screens per screen of the face, setting every hazard's strength); and the trial screen with its pieces.
    /// Screens that hold another face's timed door (Physics) stay calm too, so a door's timing is never thrown off.
    /// Hazards keep every way open: the areas only slow or deflect (PlayerMover bounds them), and a crevice keeps off both
    /// exit lanes and the edge band, clear of walls, alcoves, gate fronts and every other spot something stands on, and
    /// never cuts off any part of its screen (a flood fill over the screen checks it). FindProblems checks it all for the
    /// sweep. Uses its own SeededRng stream; each screen's hazard draws from that stream with the screen in the seed.
    /// </summary>
    public sealed class EarthPlan
    {
        /// <summary>PCG32 stream for the Earth face plan (see RngStreams).</summary>
        public const ulong RngStream = RngStreams.EarthPlan;

        /// <summary>Currency the Earth trial pays out (fired with TrialRoom.Completed).</summary>
        public const int TrialCurrency = 30;

        /// <summary>The highest hazard level.</summary>
        public const int MaxLevel = 3;

        /// <summary>Hazard screens per level step on a face of CubeSettings.TunedScreensPerFace screens (scaled per screen).</summary>
        public const int TunedHazardScreensPerLevel = 2;

        public static int HazardScreensPerLevel(int faceSize) => CubeSettings.PerScreen(TunedHazardScreensPerLevel, faceSize);

        /// <summary>
        /// The difficulty parameter: the hazard level of a face of faceSize x faceSize screens with this many hazard screens,
        /// one plus one for every HazardScreensPerLevel hazard screens beyond the first, at most MaxLevel (0 without any).
        /// </summary>
        public static int LevelFor(int hazardScreens, int faceSize) =>
            hazardScreens <= 0 ? 0 : Mathf.Clamp(1 + (hazardScreens - 1) / HazardScreensPerLevel(faceSize), 1, MaxLevel);

        /// <summary>A wind band's push at a level, as a fraction of the player's speed (well under PlayerMover.MaxPushFraction).</summary>
        public static float WindPush(int level) => 0.22f + 0.06f * (Mathf.Clamp(level, 1, MaxLevel) - 1);

        /// <summary>Freezing rain's speed multiplier at a level (well over PlayerMover.MinSpeedMultiplier).</summary>
        public static float RainSpeed(int level) => 0.75f - 0.06f * (Mathf.Clamp(level, 1, MaxLevel) - 1);

        /// <summary>The hazard kinds dealt round the hazard screens (4.10: snow is gone, the ice screens slide instead).</summary>
        public static readonly HazardKind[] Kinds = { HazardKind.Wind, HazardKind.FreezingRain, HazardKind.Crevice };

        /// <summary>The strength of a hazard kind at a level (0 for a crevice).</summary>
        public static float StrengthOf(HazardKind kind, int level)
        {
            switch (kind)
            {
                case HazardKind.Wind: return WindPush(level);
                case HazardKind.FreezingRain: return RainSpeed(level);
                default: return 0f;
            }
        }

        /// <summary>The calm eye: the face's centre screen ((1,1) at 3x3).</summary>
        public static Vector2Int EyeCell(int faceSize) => new Vector2Int(faceSize / 2, faceSize / 2);

        // Hazard geometry (local to the screen centre; authored for the first 16 x 10 screens, scaled by ScreenMath.ContentScale).
        /// <summary>Hazard areas stay inside this half-extent (inside the screen, clear of its very edge).</summary>
        public static readonly Vector2 AreaHalf = new Vector2(7f, 4.4f) * ScreenMath.ContentScale;

        public const float WindBandWidth = 2.4f * ScreenMath.ContentScale;
        public static readonly Vector2 RainSize = new Vector2(6f, 4f) * ScreenMath.ContentScale;

        /// <summary>A crevice strip's lengths, longest first (a shorter one is tried where a longer one fits nowhere), and its thickness.</summary>
        public static readonly float[] CreviceLengths = { 3f * ScreenMath.ContentScale, 2.4f * ScreenMath.ContentScale, 1.8f * ScreenMath.ContentScale };

        public const float CreviceThickness = 0.6f * ScreenMath.ContentScale;

        /// <summary>Room a crevice keeps from an exit lane and the edge band (on top of ScreenModule.KeepsExitsOpen).</summary>
        public const float CreviceLaneMargin = 0.1f * ScreenMath.ContentScale;

        /// <summary>
        /// Room a crevice keeps from walls and alcoves (a little: whether a gap is left that the player cannot pass is the
        /// flood fill's to judge, and it rejects any strip that cuts a spot off).
        /// </summary>
        public const float CreviceWallClearance = 0.3f * ScreenMath.ContentScale;

        /// <summary>Room a crevice keeps from a spot something stands on (a gate front, a slot, a trial piece).</summary>
        public const float CreviceMarkClearance = 0.5f * ScreenMath.ContentScale;

        /// <summary>How far in front of a gate's opening (toward the lane) its front reaches, and the room others keep from it.</summary>
        public const float FrontDepth = 0.6f * ScreenMath.ContentScale, FrontRadius = 1f * ScreenMath.ContentScale;

        /// <summary>Crevice centres are tried on a grid of this step over the screen inside its edge band (CreviceBand).</summary>
        public const float CreviceStep = 0.5f * ScreenMath.ContentScale;

        private static Vector2 CreviceBand => ScreenMath.DefaultScreenSize / 2f - Vector2.one * ScreenModule.EdgeClearance;

        /// <summary>The connectivity grid's step.</summary>
        public const float GridStep = 0.25f * ScreenMath.ContentScale;

        private readonly List<EarthGateSpec> gates;
        private readonly List<HazardSpec> hazards;
        private readonly List<ScreenAddress> calm;
        private readonly List<ScreenAddress> ice;
        private readonly Func<ScreenAddress, ScreenModule> moduleAt;
        private readonly Dictionary<ScreenAddress, List<ScreenMark>> others;
        private readonly ItemPlacement placement;

        public int Seed { get; }
        public FaceId Face { get; }
        public int FaceSize { get; }

        /// <summary>The run's rolled phenomenon (None when the catalog's Earth variants are unknown).</summary>
        public Phenomenon Rolled { get; }

        public IReadOnlyList<EarthGateSpec> Gates => gates;
        public IReadOnlyList<HazardSpec> Hazards => hazards;

        /// <summary>The calm eye's screen (the face's centre).</summary>
        public ScreenAddress Eye { get; }

        /// <summary>Screens with no hazard: the eye, and any that holds another face's timed door.</summary>
        public IReadOnlyList<ScreenAddress> CalmScreens => calm;

        /// <summary>The difficulty: the hazard level (LevelFor the hazard screens), which sets every hazard's strength.</summary>
        public int Level { get; }

        /// <summary>The Earth screen that holds the trial.</summary>
        public ScreenAddress TrialScreen { get; }

        /// <summary>The trial's piece spots (local to its screen centre), in striking order; null without modules or when it fits nowhere.</summary>
        public IReadOnlyList<Vector2> TrialSpots { get; }

        /// <summary>True when the plan was made with modules (so crevices and the trial are placed).</summary>
        public bool HasLayout => moduleAt != null;

        private EarthPlan(int seed, FaceId face, int faceSize, Phenomenon rolled, List<EarthGateSpec> gates,
            List<HazardSpec> hazards, List<ScreenAddress> calm, List<ScreenAddress> ice, ScreenAddress eye, int level, ScreenAddress trialScreen,
            IReadOnlyList<Vector2> trialSpots, Func<ScreenAddress, ScreenModule> moduleAt, Dictionary<ScreenAddress, List<ScreenMark>> others,
            ItemPlacement placement)
        {
            this.placement = placement;
            Seed = seed;
            Face = face;
            FaceSize = faceSize;
            Rolled = rolled;
            this.gates = gates;
            this.hazards = hazards;
            this.calm = calm;
            this.ice = ice ?? new List<ScreenAddress>();
            Eye = eye;
            Level = level;
            TrialScreen = trialScreen;
            TrialSpots = trialSpots;
            this.moduleAt = moduleAt;
            this.others = others;
        }

        /// <summary>A copy with one hazard replaced (tests of the sweep rules).</summary>
        public EarthPlan WithHazard(int index, HazardSpec hazard)
        {
            var list = new List<HazardSpec>(hazards) { [index] = hazard };
            return new EarthPlan(Seed, Face, FaceSize, Rolled, gates, list, calm, ice, Eye, Level, TrialScreen, TrialSpots, moduleAt, others, placement);
        }

        /// <summary>A copy with one gate replaced (tests of the sweep rules).</summary>
        public EarthPlan WithGate(int index, EarthGateSpec gate)
        {
            var list = new List<EarthGateSpec>(gates) { [index] = gate };
            return new EarthPlan(Seed, Face, FaceSize, Rolled, list, hazards, calm, ice, Eye, Level, TrialScreen, TrialSpots, moduleAt, others, placement);
        }

        public bool TryGetGate(ScreenAddress screen, int slot, bool optional, out EarthGateSpec spec)
        {
            foreach (EarthGateSpec s in gates)
            {
                if (s.Screen != screen || s.Slot != slot || s.Optional != optional) continue;
                spec = s;
                return true;
            }
            spec = default;
            return false;
        }

        /// <summary>The run's Earth plan as EarthFace makes it at the reveal: only with a rolled Earth item and a placement, else null.</summary>
        public static EarthPlan ForRun(CubeModel model, ItemPlacement placement, ItemDefinition wand,
            Func<ScreenAddress, ScreenModule> moduleAt, IReadOnlyList<Phenomenon> earthVariantKinds,
            IReadOnlyList<BeakKind> biologyVariantKinds, IReadOnlyList<IsotopeStage> chemistryVariantKinds) =>
            wand != null && placement != null
                ? Create(model, placement, moduleAt, earthVariantKinds, biologyVariantKinds, chemistryVariantKinds)
                : null;

        /// <summary>Whether EarthFace builds a planned gate for an item: a phenomenon gate that only that Stormcaller* opens.</summary>
        public static bool Builds(EarthGateSpec spec, ItemDefinition item) =>
            spec.Kind != Phenomenon.None && StormcallerItem.KindOf(item) == spec.Kind;

        /// <summary>The look (GateLooks) of the gate EarthFace builds at a slot for an item, or null where it builds none.</summary>
        public string GateLook(ScreenAddress screen, int slot, bool optional, ItemDefinition item) =>
            TryGetGate(screen, slot, optional, out EarthGateSpec spec) && Builds(spec, item) ? GateLooks.Of(spec.Kind) : null;

        /// <summary>The hazard on a screen, or null (calm, or not on the Earth face).</summary>
        public HazardSpec HazardOn(ScreenAddress screen)
        {
            foreach (HazardSpec h in hazards)
                if (h.Screen == screen) return h;
            return null;
        }

        public bool IsCalm(ScreenAddress screen) => calm.Contains(screen);

        /// <summary>The ice screens (4.10, IcePlan's puzzles: no hazard and no trial there); empty without modules.</summary>
        public IReadOnlyList<ScreenAddress> IceScreens => ice;

        public bool IsIce(ScreenAddress screen) => ice.Contains(screen);

        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(Rolled).Append(" eye@").Append(Eye).Append(" level ").Append(Level).Append(" trial@").Append(TrialScreen);
            if (TrialSpots != null)
            {
                sb.Append(" [");
                foreach (Vector2 p in TrialSpots)
                    sb.Append(p.x.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                        .Append(p.y.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ');
                sb.Append(']');
            }
            sb.Append("; calm");
            foreach (ScreenAddress s in calm) sb.Append(' ').Append(s);
            if (ice.Count > 0)
            {
                sb.Append("; ice");
                foreach (ScreenAddress s in ice) sb.Append(' ').Append(s);
            }
            sb.Append("; ");
            foreach (EarthGateSpec g in gates) sb.Append(g).Append("; ");
            foreach (HazardSpec h in hazards) sb.Append(h).Append("; ");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Create

        /// <summary>
        /// The plan of a run; null when Earth is not an item of the placement (no Earth pickup). earthVariantKinds maps each
        /// catalog variant index to its phenomenon, read from the items' ids (StormcallerItem.VariantKinds; null: the rolled
        /// phenomenon stays None). With moduleAt (prefabs in the sweep, instances at runtime) crevices and the trial are
        /// placed; pass the Biology beak kinds and the Chemistry stone kinds so their interactables on the face are known.
        /// </summary>
        public static EarthPlan Create(CubeModel model, ItemPlacement placement, Func<ScreenAddress, ScreenModule> moduleAt = null,
            IReadOnlyList<Phenomenon> earthVariantKinds = null, IReadOnlyList<BeakKind> biologyVariantKinds = null,
            IReadOnlyList<IsotopeStage> chemistryVariantKinds = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (placement == null || !placement.TryGetPickup(Theme.EarthAtmosphere, out _)) return null;
            FaceId face = model.FaceOf(Theme.EarthAtmosphere);
            int n = model.FaceSize;
            var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), RngStream);
            Phenomenon rolled = KindOfVariant(earthVariantKinds, placement.RolledVariant(Theme.EarthAtmosphere));

            // Gates: every required one the rolled phenomenon's; the optional ones their own phenomenon's.
            var specs = new List<EarthGateSpec>();
            foreach (GatePlacement g in placement.Gates)
                if (g.Item == Theme.EarthAtmosphere)
                    specs.Add(new EarthGateSpec(g.Screen, g.Slot, rolled, g.Screen.Face == face, false));
            foreach (OptionalGatePlacement g in placement.OptionalGates)
            {
                if (g.Item != Theme.EarthAtmosphere) continue;
                Phenomenon kind = KindOfVariant(earthVariantKinds, g.Variant);
                // Unknown or malformed data: never let an optional gate use the rolled wand, or no wand at all.
                if (kind == Phenomenon.None || kind == rolled) continue;
                specs.Add(new EarthGateSpec(g.Screen, g.Slot, kind, g.Screen.Face == face, true));
            }

            // Calm screens: the eye, and every screen holding a Physics timed door (its timing assumes a calm walk).
            var eye = new ScreenAddress(face, EyeCell(n));
            var calm = new List<ScreenAddress> { eye };
            foreach (GatePlacement g in placement.Gates)
                if (g.Item == Theme.Physics && g.Screen.Face == face && !calm.Contains(g.Screen)) calm.Add(g.Screen);
            // 4.10: the ice screens (Earth's whole-screen puzzle module) are puzzles, not hazard screens: no hazard, no trial.
            var ice = new List<ScreenAddress>();
            var hazardScreens = new List<ScreenAddress>();
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var s = new ScreenAddress(face, x, y);
                    ScreenModule module = moduleAt?.Invoke(s);
                    if (module != null && module.IsIceScreen && !calm.Contains(s)) ice.Add(s);
                    else if (!calm.Contains(s)) hazardScreens.Add(s);
                }
            rng.Shuffle(hazardScreens);
            var kinds = new List<HazardKind>(Kinds);
            rng.Shuffle(kinds);
            int level = LevelFor(hazardScreens.Count, n);

            // The trial: on the first hazard screen (in a seeded order) where its pieces fit, else on any Earth screen.
            var trialOrder = new List<ScreenAddress>(hazardScreens);
            rng.Shuffle(trialOrder);
            foreach (ScreenAddress s in calm)
                if (!TownSquare.IsTown(s, n)) trialOrder.Add(s); // the calm eye is the town (4.5): no trial there
            ScreenAddress trialScreen = trialOrder[0];
            List<Vector2> trialSpots = null;
            Dictionary<ScreenAddress, List<ScreenMark>> others = null;
            if (moduleAt != null)
            {
                others = OtherFaceMarks(model, placement, moduleAt, biologyVariantKinds, chemistryVariantKinds);
                foreach (ScreenAddress t in trialOrder)
                {
                    ScreenModule module = moduleAt(t);
                    if (module == null) continue;
                    List<Vector2> spots = TrialSpotsOn(model.Seed, t, module, MarksFor(t, module, specs, others, null));
                    if (spots.Count < EarthTrial.PieceCount) continue;
                    trialScreen = t;
                    trialSpots = spots;
                    break;
                }
            }

            var hazards = new List<HazardSpec>();
            for (int i = 0; i < hazardScreens.Count; i++)
            {
                ScreenAddress s = hazardScreens[i];
                HazardKind kind = kinds[i % kinds.Count];
                var screenRng = new SeededRng(ScreenSeed(model.Seed, s), RngStream);
                if (kind == HazardKind.Crevice)
                {
                    Rect strip = default;
                    if (moduleAt != null)
                    {
                        ScreenModule module = moduleAt(s);
                        List<ScreenMark> marks = MarksFor(s, module, specs, others, s == trialScreen ? trialSpots : null);
                        strip = module != null ? PlaceCrevice(Solids(module, s == trialScreen ? trialSpots : null), marks, screenRng) : default;
                        // No room for a crevice on this screen: a wind band there instead (it never blocks anything).
                        if (strip.width <= 0f) kind = HazardKind.Wind;
                    }
                    if (kind == HazardKind.Crevice)
                    {
                        hazards.Add(new HazardSpec(s, HazardKind.Crevice, strip, Vector2.zero, 0f));
                        continue;
                    }
                }
                hazards.Add(AreaHazard(s, kind, level, screenRng));
            }
            return new EarthPlan(model.Seed, face, n, rolled, specs, hazards, calm, ice, eye, level, trialScreen, trialSpots, moduleAt, others, placement);
        }

        /// <summary>The phenomenon of a variant index from the id-derived kinds; None when unknown.</summary>
        private static Phenomenon KindOfVariant(IReadOnlyList<Phenomenon> kinds, int variant) =>
            kinds != null && variant >= 0 && variant < kinds.Count ? kinds[variant] : Phenomenon.None;

        private static ulong ScreenSeed(int seed, ScreenAddress s) =>
            ((ulong)(uint)seed << 32) | (uint)((int)s.Face * 64 + s.Cell.x * 8 + s.Cell.y) | (0x45u << 16);

        /// <summary>Room a trial piece keeps from a mark (a gate front, a slot, a button or plate), beyond both radii.</summary>
        public const float TrialMarkClearance = 0.3f * ScreenMath.ContentScale;

        /// <summary>
        /// The trial's piece spots on a module (local): lane-side spots clear of every mark (gate fronts, hidden-item and
        /// core-entrance slots, other faces' buttons, plates and flowers), picked from the plan stream keyed by the screen.
        /// </summary>
        public static List<Vector2> TrialSpotsOn(int seed, ScreenAddress screen, ScreenModule module, IReadOnlyList<ScreenMark> marks)
        {
            var rng = new SeededRng(ScreenSeed(seed, screen) ^ 0x7472000000000000UL, RngStream);
            List<Vector2> candidates = BiologyTrial.LaneSideSpots(TownPlan.BlockedRects(module));
            if (marks != null) candidates.RemoveAll(c => TrialSpotProblem(c, marks) != null);
            return EarthTrial.PickSpots(candidates, EarthTrial.PieceCount, rng);
        }

        /// <summary>What a trial piece at a spot (local) would sit on, or null.</summary>
        public static string TrialSpotProblem(Vector2 spot, IReadOnlyList<ScreenMark> marks)
        {
            foreach (ScreenMark m in marks)
                if (Vector2.Distance(spot, m.Local) < m.Radius + BiologyTrial.PieceBoundRadius + TrialMarkClearance - 1e-4f)
                    return $"a trial piece at {spot} sits on {m.What}";
            return null;
        }

        /// <summary>The box of a trial piece at a spot (local): solid for the quake's and the tornado's pieces.</summary>
        public static Rect TrialPieceRect(Vector2 spot) => CentredOn(spot, new Vector2(EarthTrialPiece.Size, EarthTrialPiece.Size));

        /// <summary>
        /// What the player cannot walk through on a screen (local): its module's walls and alcoves, plus the trial's pieces
        /// when they are on it (counted solid whatever the phenomenon, so the check holds for any run's wand).
        /// </summary>
        private static List<Rect> Solids(ScreenModule module, IReadOnlyList<Vector2> trialSpots)
        {
            List<Rect> solids = TownPlan.BlockedRects(module);
            if (trialSpots != null)
                foreach (Vector2 p in trialSpots) solids.Add(TrialPieceRect(p));
            return solids;
        }

        /// <summary>A wind band or rain area on a screen, its place drawn from the screen's stream.</summary>
        private static HazardSpec AreaHazard(ScreenAddress s, HazardKind kind, int level, SeededRng rng)
        {
            float strength = StrengthOf(kind, level);
            switch (kind)
            {
                case HazardKind.Wind:
                {
                    // A band across the screen, off-centre, pushing along itself: it deflects a walk through it.
                    bool horizontal = rng.NextInt(2) == 0;
                    float side = rng.NextInt(2) == 0 ? 1f : -1f;
                    float along = rng.NextInt(2) == 0 ? 1f : -1f;
                    Rect area;
                    Vector2 dir;
                    if (horizontal)
                    {
                        float cy = side * 2.6f * ScreenMath.ContentScale;
                        area = Rect.MinMaxRect(-AreaHalf.x, cy - WindBandWidth / 2f, AreaHalf.x, cy + WindBandWidth / 2f);
                        dir = new Vector2(along, 0f);
                    }
                    else
                    {
                        float cx = side * 4.2f * ScreenMath.ContentScale;
                        area = Rect.MinMaxRect(cx - WindBandWidth / 2f, -AreaHalf.y, cx + WindBandWidth / 2f, AreaHalf.y);
                        dir = new Vector2(0f, along);
                    }
                    return new HazardSpec(s, kind, Clip(area), dir, strength);
                }
                default:
                    return new HazardSpec(s, kind, Clip(CentredOn(LanePatchCentre(rng, 3f * ScreenMath.ContentScale, 1.8f * ScreenMath.ContentScale), RainSize)), Vector2.zero, strength);
            }
        }

        /// <summary>A patch centre on one of the four half-lanes (so the patch lies across a way through the screen).</summary>
        private static Vector2 LanePatchCentre(SeededRng rng, float x, float y)
        {
            switch (rng.NextInt(4))
            {
                case 0: return new Vector2(x, 0f);
                case 1: return new Vector2(-x, 0f);
                case 2: return new Vector2(0f, y);
                default: return new Vector2(0f, -y);
            }
        }

        private static Rect CentredOn(Vector2 c, Vector2 size) => new Rect(c - size / 2f, size);

        private static Rect Clip(Rect r) =>
            Rect.MinMaxRect(Mathf.Max(r.xMin, -AreaHalf.x), Mathf.Max(r.yMin, -AreaHalf.y),
                Mathf.Min(r.xMax, AreaHalf.x), Mathf.Min(r.yMax, AreaHalf.y));

        /// <summary>
        /// The spots a crevice keeps clear of on a screen: every gate front (whatever gate stands there), the module's
        /// hidden-item and core-entrance slots, the trial's pieces (on the trial screen) and other faces' interactables.
        /// </summary>
        private static List<ScreenMark> MarksFor(ScreenAddress screen, ScreenModule module, List<EarthGateSpec> specs,
            Dictionary<ScreenAddress, List<ScreenMark>> others, IReadOnlyList<Vector2> trialSpots)
        {
            var list = others != null && others.TryGetValue(screen, out List<ScreenMark> o) ? new List<ScreenMark>(o) : new List<ScreenMark>();
            if (module != null)
            {
                Vector2 origin = module.transform.position;
                foreach (GateSlot slot in module.Gates)
                    list.Add(new ScreenMark("gate front", FrontLocal((Vector2)slot.transform.position - origin, slot.PocketOffset), FrontRadius));
                foreach (HiddenItemSlot h in module.HiddenItems)
                    list.Add(new ScreenMark("hidden-item slot", (Vector2)h.transform.position - origin, 0.6f * ScreenMath.ContentScale));
                foreach (CoreEntranceSlot c in module.CoreEntrances)
                    list.Add(new ScreenMark("core-entrance slot", (Vector2)c.transform.position - origin, 2f * ScreenMath.ContentScale));
            }
            if (trialSpots != null)
                foreach (Vector2 p in trialSpots) list.Add(new ScreenMark("Earth trial piece", p, BiologyTrial.PieceBoundRadius));
            list.Add(new ScreenMark("open pickup", CubeWorld.OpenPickupOffset, ItemPickup.Radius));
            return list;
        }

        /// <summary>The front of a gate in a slot (local): just in front of its opening, toward the lane.</summary>
        public static Vector2 FrontLocal(Vector2 slotLocal, Vector2 pocketOffset)
        {
            Vector2 into = pocketOffset.sqrMagnitude > 1e-6f ? pocketOffset.normalized : Vector2.zero;
            return slotLocal - into * FrontDepth;
        }

        /// <summary>
        /// A crevice strip on a screen: a seeded candidate (long strips first, then short ones, either way round) whose strip
        /// keeps off both exit lanes and the edge band (with CreviceLaneMargin), CreviceWallClearance from every wall and
        /// alcove, clear of every mark, and cuts nothing off (CutOff). Empty when none fits.
        /// </summary>
        private static Rect PlaceCrevice(List<Rect> blocked, List<ScreenMark> marks, SeededRng rng)
        {
            bool[] baseline = null;
            foreach (float length in CreviceLengths)
            {
                var candidates = new List<Rect>();
                int nx = Mathf.FloorToInt(CreviceBand.x / CreviceStep + 1e-4f), ny = Mathf.FloorToInt(CreviceBand.y / CreviceStep + 1e-4f);
                for (int i = -nx; i <= nx; i++)
                    for (int j = -ny; j <= ny; j++)
                    {
                        var c = new Vector2(i * CreviceStep, j * CreviceStep);
                        candidates.Add(CentredOn(c, new Vector2(length, CreviceThickness)));
                        candidates.Add(CentredOn(c, new Vector2(CreviceThickness, length)));
                    }
                rng.Shuffle(candidates);
                foreach (Rect strip in candidates)
                {
                    if (StripProblem(strip, blocked, marks) != null) continue;
                    if (baseline == null) baseline = Flood(blocked, null);
                    if (CutOff(blocked, strip, baseline) == null) return strip;
                }
            }
            return default;
        }

        /// <summary>Why a crevice strip does not fit (lanes, walls, marks), or null.</summary>
        private static string StripProblem(Rect strip, List<Rect> blocked, IReadOnlyList<ScreenMark> marks)
        {
            Rect padded = Rect.MinMaxRect(strip.xMin - CreviceLaneMargin, strip.yMin - CreviceLaneMargin,
                strip.xMax + CreviceLaneMargin, strip.yMax + CreviceLaneMargin);
            if (!ScreenModule.KeepsExitsOpen(padded)) return "it runs into an exit lane or the edge band";
            foreach (Rect b in blocked)
                if (RectDistance(strip, b) < CreviceWallClearance - 1e-4f) return "it runs too close to a wall or alcove";
            if (marks != null)
                foreach (ScreenMark m in marks)
                    if (DistanceToRect(m.Local, strip) < m.Radius + CreviceMarkClearance - 1e-4f) return $"it sits on {m.What}";
            return null;
        }

        // ------------------------------------------------------------------ Connectivity

        private static readonly Vector2 GridHalf = Game.Tracer.ScreenMath.DefaultScreenSize / 2f -
                                                   new Vector2(TimeField.PlayerRadius, TimeField.PlayerRadius);
        private static readonly int GridNx = Mathf.FloorToInt(GridHalf.x * 2f / GridStep) + 1;
        private static readonly int GridNy = Mathf.FloorToInt(GridHalf.y * 2f / GridStep) + 1;

        private static Vector2 GridPos(int i, int j) => -GridHalf + new Vector2(i * GridStep, j * GridStep);

        private static bool StandsFree(Vector2 p, List<Rect> blocked, Rect? strip)
        {
            foreach (Rect b in blocked)
                if (DistanceToRect(p, b) < TimeField.PlayerRadius) return false;
            return !(strip is Rect s) || DistanceToRect(p, s) >= TimeField.PlayerRadius;
        }

        /// <summary>
        /// Where the player can stand and walk to from the screen centre, on a grid over the screen (the screen treated as a
        /// closed box: stricter than play, where its edges lead on), with the walls, alcoves and an optional crevice strip.
        /// </summary>
        private static bool[] Flood(List<Rect> blocked, Rect? strip)
        {
            var free = new bool[GridNx * GridNy];
            for (int j = 0; j < GridNy; j++)
                for (int i = 0; i < GridNx; i++)
                    free[j * GridNx + i] = StandsFree(GridPos(i, j), blocked, strip);
            var seen = new bool[free.Length];
            int start = Index(Vector2.zero);
            if (!free[start]) return seen;
            var queue = new Queue<int>();
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int k = queue.Dequeue();
                int i = k % GridNx, j = k / GridNx;
                void Visit(int a, int b)
                {
                    if (a < 0 || b < 0 || a >= GridNx || b >= GridNy) return;
                    int q = b * GridNx + a;
                    if (seen[q] || !free[q]) return;
                    seen[q] = true;
                    queue.Enqueue(q);
                }
                Visit(i + 1, j);
                Visit(i - 1, j);
                Visit(i, j + 1);
                Visit(i, j - 1);
            }
            return seen;
        }

        private static int Index(Vector2 p)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt((p.x + GridHalf.x) / GridStep), 0, GridNx - 1);
            int j = Mathf.Clamp(Mathf.RoundToInt((p.y + GridHalf.y) / GridStep), 0, GridNy - 1);
            return j * GridNx + i;
        }

        /// <summary>The four exits' ends (local), where the lanes meet the screen's edges.</summary>
        private static readonly Vector2[] ExitEnds =
        {
            new Vector2(GridHalf.x, 0f), new Vector2(-GridHalf.x, 0f), new Vector2(0f, GridHalf.y), new Vector2(0f, -GridHalf.y),
        };

        /// <summary>
        /// What a crevice strip cuts off on a screen, or null: an exit no longer reachable from the centre, or any spot the
        /// player could reach without the strip (and still can stand on with it) that it can no longer reach.
        /// </summary>
        private static string CutOff(List<Rect> blocked, Rect strip, bool[] baseline)
        {
            bool[] with = Flood(blocked, strip);
            foreach (Vector2 e in ExitEnds)
                if (!with[Index(e)]) return $"the exit at {e} can no longer be reached";
            for (int j = 0; j < GridNy; j++)
                for (int i = 0; i < GridNx; i++)
                {
                    int k = j * GridNx + i;
                    if (!baseline[k] || with[k]) continue;
                    Vector2 p = GridPos(i, j);
                    if (DistanceToRect(p, strip) < TimeField.PlayerRadius) continue; // under the strip itself
                    return $"the spot at {p} is cut off";
                }
            return null;
        }

        /// <summary>
        /// Why a crevice on a screen with this module traps the player or blocks the way (null when it does neither): it
        /// must keep off the lanes and edge band, clear of walls, alcoves and marks, and cut nothing off.
        /// </summary>
        public static string CreviceProblem(Rect strip, ScreenModule module, IReadOnlyList<ScreenMark> marks) =>
            CreviceProblem(strip, TownPlan.BlockedRects(module), marks);

        /// <summary>CreviceProblem over given blocked rects (walls and alcoves, local to the screen centre).</summary>
        public static string CreviceProblem(Rect strip, List<Rect> blocked, IReadOnlyList<ScreenMark> marks)
        {
            blocked = blocked ?? new List<Rect>();
            string problem = StripProblem(strip, blocked, marks);
            return problem ?? CutOff(blocked, strip, Flood(blocked, null));
        }

        private static float DistanceToRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float RectDistance(Rect a, Rect b)
        {
            float dx = Mathf.Max(b.xMin - a.xMax, 0f, a.xMin - b.xMax);
            float dy = Mathf.Max(b.yMin - a.yMax, 0f, a.yMin - b.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // ------------------------------------------------------------------ Marks

        /// <summary>
        /// Spots other faces' content uses on the Earth face's screens (local): Biology buttons and flowers and Chemistry lead
        /// plates (other faces' gates' fronts are covered by the gate fronts every crevice keeps clear of).
        /// </summary>
        private static Dictionary<ScreenAddress, List<ScreenMark>> OtherFaceMarks(CubeModel model, ItemPlacement placement,
            Func<ScreenAddress, ScreenModule> moduleAt, IReadOnlyList<BeakKind> kinds, IReadOnlyList<IsotopeStage> stoneKinds)
        {
            var result = new Dictionary<ScreenAddress, List<ScreenMark>>();
            FaceId face = model.FaceOf(Theme.EarthAtmosphere);
            void Add(ScreenAddress s, string what, Vector2 at, float r)
            {
                if (s.Face != face) return;
                if (!result.TryGetValue(s, out var list)) result[s] = list = new List<ScreenMark>();
                list.Add(new ScreenMark(what, at, r));
            }
            Vector2? SlotLocal(ScreenAddress s, int slot)
            {
                ScreenModule m = moduleAt(s);
                if (m == null || slot >= m.Gates.Length) return null;
                return m.Gates[slot].transform.position - m.transform.position;
            }
            BiologyPlan biology = BiologyPlan.Create(model, placement, kinds);
            if (biology != null)
                foreach (BeakGateSpec s in biology.Gates)
                {
                    if (s.Kind == BeakGateKind.Button && SlotLocal(s.Screen, s.Slot) is Vector2 local)
                        Add(s.Screen, $"Biology button ({s})", ButtonGate.ButtonLocal(local), BeakButton.Radius);
                    if (s.Kind == BeakGateKind.FlowerVine)
                        Add(s.FlowerScreen, $"Biology flower ({s})", FlowerVineGate.FlowerLocal, PollinationFlower.Radius);
                }
            ChemistryPlan chemistry = ChemistryPlan.Create(model, placement, stoneKinds);
            if (chemistry != null)
                foreach (IsotopeGateSpec s in chemistry.Gates)
                    if (s.Stage == IsotopeStage.Lead && SlotLocal(s.Screen, s.Slot) is Vector2 local)
                        Add(s.Screen, $"lead plate ({s})", LeadPlateGate.PlateLocal(local), LeadPlate.Radius);
            return result;
        }

        /// <summary>The marks a crevice on a screen keeps clear of (for FindProblems and tests).</summary>
        public List<ScreenMark> MarksOn(ScreenAddress screen) =>
            MarksFor(screen, moduleAt != null ? moduleAt(screen) : null, gates, others, screen == TrialScreen ? TrialSpots : null);

        /// <summary>
        /// The fronts of every Earth gate (required and optional) on any face, keyed by screen: what other faces' content
        /// keeps clear of for Earth's gates (Math's layouts and spot filter). Pure: needs only the placement and modules.
        /// </summary>
        public static Dictionary<ScreenAddress, List<ScreenMark>> GateFrontMarks(ItemPlacement placement, Func<ScreenAddress, ScreenModule> moduleAt)
        {
            var result = new Dictionary<ScreenAddress, List<ScreenMark>>();
            if (placement == null || moduleAt == null) return result;
            void Add(ScreenAddress s, int slot, string what)
            {
                ScreenModule m = moduleAt(s);
                if (m == null || slot >= m.Gates.Length) return;
                GateSlot g = m.Gates[slot];
                if (!result.TryGetValue(s, out var list)) result[s] = list = new List<ScreenMark>();
                list.Add(new ScreenMark(what, FrontLocal((Vector2)g.transform.position - (Vector2)m.transform.position, g.PocketOffset), FrontRadius));
            }
            foreach (GatePlacement g in placement.Gates)
                if (g.Item == Theme.EarthAtmosphere) Add(g.Screen, g.Slot, $"front of Earth {g}");
            foreach (OptionalGatePlacement g in placement.OptionalGates)
                if (g.Item == Theme.EarthAtmosphere) Add(g.Screen, g.Slot, $"front of Earth {g}");
            return result;
        }

        /// <summary>
        /// The marks other content must keep clear of for Earth's content (PhysicsPlan's boulders and switches, Earth's own
        /// NPCs and enemies): every Earth gate's front (required and optional, whether or not the wand kinds were known), every crevice (points
        /// along it) and the trial's pieces, by screen.
        /// </summary>
        public Dictionary<ScreenAddress, List<ScreenMark>> ContentMarks()
        {
            Dictionary<ScreenAddress, List<ScreenMark>> result = moduleAt != null
                ? GateFrontMarks(placement, moduleAt)
                : new Dictionary<ScreenAddress, List<ScreenMark>>();
            void Add(ScreenAddress s, string what, Vector2 at, float r)
            {
                if (!result.TryGetValue(s, out var list)) result[s] = list = new List<ScreenMark>();
                list.Add(new ScreenMark(what, at, r));
            }
            foreach (HazardSpec h in hazards)
            {
                if (h.Kind != HazardKind.Crevice || !h.IsPlaced) continue;
                Rect a = h.Area;
                bool across = a.width >= a.height;
                float length = across ? a.width : a.height;
                float half = (across ? a.height : a.width) / 2f;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / (0.5f * ScreenMath.ContentScale)));
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    Vector2 p = across ? new Vector2(Mathf.Lerp(a.xMin, a.xMax, t), a.center.y) : new Vector2(a.center.x, Mathf.Lerp(a.yMin, a.yMax, t));
                    Add(h.Screen, "Earth crevice", p, half);
                }
            }
            if (TrialSpots != null)
                foreach (Vector2 p in TrialSpots) Add(TrialScreen, "Earth trial piece", p, BiologyTrial.PieceBoundRadius);
            return result;
        }

        /// <summary>True when a point (local to a screen centre) is within margin of a crevice there.</summary>
        public bool NearCrevice(ScreenAddress screen, Vector2 local, float margin)
        {
            foreach (HazardSpec h in hazards)
                if (h.Screen == screen && h.Kind == HazardKind.Crevice && h.IsPlaced && DistanceToRect(local, h.Area) < margin) return true;
            return false;
        }

        // ------------------------------------------------------------------ Problems

        /// <summary>
        /// Problems with a run's Earth plan: every required Earth gate has a spec with the rolled phenomenon and every
        /// optional one another phenomenon, on Earth; the rolled phenomenon opens a gate on another face (when another face
        /// is built); with enough optional gates, every other phenomenon has one; the eye is the centre screen and calm,
        /// so is every screen with another face's timed door, and every other Earth screen has exactly one hazard; the
        /// hazard level is LevelFor the hazard screens and every hazard's strength that level's (inside PlayerMover's
        /// bounds, so nothing can stop the player); every area stays on its screen; with four or more hazard screens
        /// every kind appears. With moduleAt: every crevice is placed, keeps off the lanes and edge band, clear of walls,
        /// alcoves, gate fronts and slots, and cuts nothing off its screen; the trial fits on an Earth screen. Empty when
        /// all is well (or when Earth is not an item). That optional gates guard nothing required is the sweep's generic
        /// optional-gate rule (SeedSweep.FindOptionalGateProblems).
        /// </summary>
        public static List<string> FindProblems(CubeModel model, ItemPlacement placement, IReadOnlyList<Phenomenon> earthVariantKinds,
            Func<ScreenAddress, ScreenModule> moduleAt = null, IReadOnlyList<BeakKind> biologyVariantKinds = null,
            IReadOnlyList<IsotopeStage> chemistryVariantKinds = null)
        {
            EarthPlan plan = Create(model, placement, moduleAt, earthVariantKinds, biologyVariantKinds, chemistryVariantKinds);
            return plan == null ? new List<string>() : plan.Problems(model, placement, earthVariantKinds != null);
        }

        /// <summary>The rules of FindProblems for this plan (knownKinds: the catalog's variant kinds were given).</summary>
        public List<string> Problems(CubeModel model, ItemPlacement placement, bool knownKinds = true)
        {
            var problems = new List<string>();
            int builtFaces = 0;
            for (int f = 0; f < CubeSettings.FaceCount; f++)
                if (CubeLayout.IsLaidOut(model, (FaceId)f)) builtFaces++;

            // Gates.
            if (knownKinds && Rolled == Phenomenon.None) problems.Add("the run has no Stormcaller* phenomenon");
            int required = 0, optional = 0;
            foreach (GatePlacement g in placement.Gates)
            {
                if (g.Item != Theme.EarthAtmosphere) continue;
                required++;
                if (!TryGetGate(g.Screen, g.Slot, false, out _)) problems.Add($"{g} has no Earth gate");
            }
            foreach (OptionalGatePlacement g in placement.OptionalGates)
            {
                if (g.Item != Theme.EarthAtmosphere) continue;
                optional++;
                if (knownKinds && !TryGetGate(g.Screen, g.Slot, true, out _)) problems.Add($"{g} has no Earth gate");
            }
            if (knownKinds && required + optional != gates.Count)
                problems.Add($"{gates.Count} Earth gates for {required} required and {optional} optional placed");
            int offHome = 0;
            foreach (EarthGateSpec s in gates)
            {
                if (s.OnHome != (s.Screen.Face == Face)) problems.Add($"{s}: wrong home flag");
                if (!s.Optional && s.Kind != Rolled) problems.Add($"{s}: a required gate needs another phenomenon");
                if (s.Optional && (s.Kind == Rolled || s.Kind == Phenomenon.None)) problems.Add($"{s}: an optional gate uses the rolled phenomenon");
                if (s.Optional && !s.OnHome) problems.Add($"{s}: optional Earth gates belong on Earth");
                if (!s.Optional && !s.OnHome) offHome++;
            }
            if (offHome < 1 && builtFaces > 1) problems.Add($"the {Rolled} Stormcaller* opens no gate on another face");
            if (knownKinds && optional >= StormcallerItem.VariantCount - 1)
                foreach (Phenomenon other in StormcallerItem.VariantOrder)
                {
                    if (other == Rolled) continue;
                    bool has = false;
                    foreach (EarthGateSpec s in gates)
                        if (s.Optional && s.Kind == other) has = true;
                    if (!has) problems.Add($"no optional {other} gate (the other phenomena's gates are extras)");
                }

            // Calm eye and hazards.
            if (Eye != new ScreenAddress(Face, EyeCell(FaceSize))) problems.Add($"the calm eye is on {Eye}, not the centre screen");
            if (HazardOn(Eye) != null) problems.Add($"the calm eye {Eye} has a hazard");
            foreach (GatePlacement g in placement.Gates)
                if (g.Item == Theme.Physics && g.Screen.Face == Face && HazardOn(g.Screen) != null)
                    problems.Add($"{g.Screen} has a hazard and a timed door");
            int hazardScreens = 0;
            var seenKinds = new HashSet<HazardKind>();
            for (int y = 0; y < FaceSize; y++)
                for (int x = 0; x < FaceSize; x++)
                {
                    var s = new ScreenAddress(Face, x, y);
                    int count = 0;
                    foreach (HazardSpec h in hazards)
                        if (h.Screen == s) count++;
                    if (IsCalm(s) || IsIce(s))
                    {
                        if (count != 0) problems.Add($"{(IsIce(s) ? "the ice screen" : "calm")} {s} has {count} hazards");
                        continue;
                    }
                    if (HasLayout && moduleAt(s) != null && moduleAt(s).IsIceScreen) problems.Add($"{s} is an ice screen the plan missed");
                    hazardScreens++;
                    if (count != 1) problems.Add($"{s} has {count} hazards (want one)");
                }
            int level = LevelFor(hazardScreens, FaceSize);
            if (Level != level) problems.Add($"hazard level {Level}, want {level}");
            foreach (HazardSpec h in hazards)
            {
                seenKinds.Add(h.Kind);
                if (h.Screen.Face != Face) problems.Add($"{h}: not on the Earth face");
                if (Mathf.Abs(h.Strength - StrengthOf(h.Kind, Level)) > 1e-4f) problems.Add($"{h}: wrong strength for level {Level}");
                switch (h.Kind)
                {
                    case HazardKind.Wind:
                        if (h.Strength <= 0f || h.Strength >= Game.Tracer.PlayerMover.MaxPushFraction)
                            problems.Add($"{h}: a push that strong could stop the player");
                        if (Mathf.Abs(h.Direction.magnitude - 1f) > 1e-3f) problems.Add($"{h}: no direction");
                        break;
                    case HazardKind.FreezingRain:
                        if (h.Strength < Game.Tracer.PlayerMover.MinSpeedMultiplier || h.Strength >= 1f)
                            problems.Add($"{h}: slows below the minimum speed (or not at all)");
                        break;
                }
                if (h.Kind != HazardKind.Crevice || HasLayout)
                {
                    if (!h.IsPlaced) problems.Add($"{h}: not placed");
                    else if (h.Area.xMin < -AreaHalf.x - 1e-4f || h.Area.xMax > AreaHalf.x + 1e-4f ||
                             h.Area.yMin < -AreaHalf.y - 1e-4f || h.Area.yMax > AreaHalf.y + 1e-4f)
                        problems.Add($"{h}: off its screen");
                }
                if (h.Kind == HazardKind.Crevice && HasLayout && h.IsPlaced)
                {
                    string why = CreviceProblem(h.Area, Solids(moduleAt(h.Screen), h.Screen == TrialScreen ? TrialSpots : null), MarksOn(h.Screen));
                    if (why != null) problems.Add($"{h}: {why}");
                }
            }
            if (hazardScreens >= Kinds.Length)
                foreach (HazardKind k in Kinds)
                    if (!seenKinds.Contains(k)) problems.Add($"no {k} on the face");

            // Trial.
            if (TrialScreen.Face != Face) problems.Add($"the trial is on {TrialScreen}, not on Earth");
            if (IsIce(TrialScreen)) problems.Add($"the trial is on the ice screen {TrialScreen}");
            if (TownSquare.IsTown(TrialScreen, FaceSize)) problems.Add($"the trial is on the town {TrialScreen} (the calm eye)");
            if (HasLayout)
            {
                if (TrialSpots == null || TrialSpots.Count < EarthTrial.PieceCount) problems.Add("the trial fits on no Earth screen");
                else
                {
                    ScreenModule module = moduleAt(TrialScreen);
                    List<ScreenMark> marks = MarksFor(TrialScreen, module, gates, others, null);
                    List<Vector2> again = module != null ? TrialSpotsOn(Seed, TrialScreen, module, marks) : null;
                    if (again == null || again.Count != TrialSpots.Count) problems.Add($"the trial's pieces do not fit on {TrialScreen}");
                    foreach (Vector2 p in TrialSpots)
                    {
                        string why = TrialSpotProblem(p, marks);
                        if (why != null) problems.Add(why);
                    }
                    for (int a = 0; a < TrialSpots.Count; a++)
                        for (int b = a + 1; b < TrialSpots.Count; b++)
                            if (Vector2.Distance(TrialSpots[a], TrialSpots[b]) < EarthTrial.PieceSpacing - 1e-4f)
                                problems.Add($"two trial pieces on {TrialScreen} are too close for single swings");
                }
            }
            return problems;
        }
    }
}
