using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>PhysicsPlan's layout: the per-screen contexts and the builder that places doors, the trial and boulders.</summary>
    public sealed partial class PhysicsPlan
    {
        private sealed class ScreenContext
        {
            public readonly List<Rect> Blocked;
            public readonly List<ScreenMark> Others;

            public ScreenContext(List<Rect> blocked, List<ScreenMark> others)
            {
                Blocked = blocked;
                Others = others;
            }
        }

        /// <summary>Lays out doors, the trial and boulders, keeping per-screen contexts for the checks.</summary>
        private sealed class LayoutBuilder
        {
            private readonly CubeModel model;
            private readonly ItemPlacement placement;
            private readonly Func<ScreenAddress, ScreenModule> moduleAt;
            private readonly Dictionary<ScreenAddress, List<ScreenMark>> others;
            public readonly Dictionary<ScreenAddress, ScreenContext> Contexts = new Dictionary<ScreenAddress, ScreenContext>();

            public LayoutBuilder(CubeModel model, ItemPlacement placement, Func<ScreenAddress, ScreenModule> moduleAt,
                IReadOnlyList<BeakKind> kinds)
            {
                this.model = model;
                this.placement = placement;
                this.moduleAt = moduleAt;
                others = OtherFaceMarks(model, placement, moduleAt, kinds);
            }

            private ScreenContext Ctx(ScreenAddress screen)
            {
                if (Contexts.TryGetValue(screen, out ScreenContext c)) return c;
                ScreenModule module = moduleAt(screen);
                var marks = others.TryGetValue(screen, out List<ScreenMark> list) ? new List<ScreenMark>(list) : new List<ScreenMark>();
                if (module != null)
                {
                    Vector2 origin = module.transform.position;
                    foreach (HiddenItemSlot h in module.HiddenItems)
                        marks.Add(new ScreenMark("hidden-item slot", (Vector2)h.transform.position - origin, Boulder.Radius));
                    foreach (CoreEntranceSlot core in module.CoreEntrances)
                        marks.Add(new ScreenMark("core-entrance slot", (Vector2)core.transform.position - origin, Boulder.Radius));
                }
                c = new ScreenContext(TownPlan.BlockedRects(module), marks);
                Contexts[screen] = c;
                return c;
            }

            public void Lay(List<TimedDoorSpec> specs, List<ScreenAddress> trialOrder, out ScreenAddress trialScreen,
                out PhysicsTrialSpec trial)
            {
                // 1. Doors and their switches (fixed candidate order: no randomness).
                var own = new Dictionary<ScreenAddress, List<ScreenMark>>();
                List<ScreenMark> Own(ScreenAddress s) => own.TryGetValue(s, out var l) ? l : own[s] = new List<ScreenMark>();
                for (int i = 0; i < specs.Count; i++)
                {
                    TimedDoorSpec s = specs[i];
                    ScreenModule module = moduleAt(s.Screen);
                    if (module == null || s.Slot >= module.Gates.Length) continue;
                    ScreenContext ctx = Ctx(s.Screen);
                    DoorLayout door = SlotDoor(module, s.Slot, s.Required);
                    door = WithSwitch(door, ctx, Own(s.Screen), null);
                    specs[i] = s.WithLayout(door);
                    AddDoorMarks(Own(s.Screen), door, s.ToString());
                }

                // 2. The trial, on the first screen in the seeded order where it and every door's boulders fit.
                trialScreen = trialOrder[0];
                trial = null;
                Dictionary<int, List<Vector2>> trialDoorBoulders = null;
                foreach (ScreenAddress t in trialOrder)
                {
                    if (moduleAt(t) == null) continue;
                    var rng = new SeededRng(ScreenSeed(t, 1), RngStream);
                    PhysicsTrialSpec attempt = TryTrial(t, specs, Own(t), rng, out Dictionary<int, List<Vector2>> boulders,
                        out List<TimedDoorSpec> adjusted);
                    if (attempt == null) continue;
                    trial = attempt;
                    trialScreen = t;
                    trialDoorBoulders = boulders;
                    for (int i = 0; i < specs.Count; i++) specs[i] = adjusted[i];
                    break;
                }

                // 3. Every other screen's boulders.
                var screens = new List<ScreenAddress>();
                foreach (TimedDoorSpec s in specs)
                    if (!screens.Contains(s.Screen)) screens.Add(s.Screen);
                foreach (ScreenAddress screen in screens)
                {
                    Dictionary<int, List<Vector2>> placed;
                    if (trial != null && screen == trial.Screen) placed = trialDoorBoulders;
                    else
                    {
                        FitBoulders(screen, specs, null, Own(screen), out List<TimedDoorSpec> adjusted, out placed, out _);
                        for (int i = 0; i < specs.Count; i++) specs[i] = adjusted[i];
                    }
                    foreach (KeyValuePair<int, List<Vector2>> e in placed) specs[e.Key] = specs[e.Key].WithBoulders(e.Value);
                }
            }

            private ulong ScreenSeed(ScreenAddress s, int use) =>
                ((ulong)(uint)model.Seed << 32) | (uint)(((int)s.Face * 64 + s.Cell.x * 8 + s.Cell.y) | (use << 16));

            /// <summary>
            /// Boulders for a screen (PlaceBoulders) with the doors' rolled counts where they fit; on a screen too
            /// cramped for them, the counts come down until they do (spares first, then the largest requirement, never
            /// below one boulder). The adjusted specs come back in a copy. False only if even one boulder per door fails.
            /// </summary>
            private bool FitBoulders(ScreenAddress screen, List<TimedDoorSpec> specs, PhysicsTrialSpec trial, List<ScreenMark> own,
                out List<TimedDoorSpec> adjusted, out Dictionary<int, List<Vector2>> placed, out List<Vector2> trialBoulders)
            {
                adjusted = new List<TimedDoorSpec>(specs);
                while (true)
                {
                    var rng = new SeededRng(ScreenSeed(screen, trial != null ? 3 : 2), RngStream);
                    if (PlaceBoulders(screen, adjusted, trial, own, rng, out placed, out trialBoulders)) return true;
                    int spare = -1, largest = -1;
                    for (int i = 0; i < adjusted.Count; i++)
                    {
                        TimedDoorSpec s = adjusted[i];
                        if (s.Screen != screen || s.Layout == null) continue;
                        if (s.Spare > 0) spare = i;
                        if (s.Required > 1 && (largest < 0 || s.Required >= adjusted[largest].Required)) largest = i;
                    }
                    if (spare >= 0) adjusted[spare] = adjusted[spare].WithCounts(adjusted[spare].Required, 0);
                    else if (largest >= 0) adjusted[largest] = adjusted[largest].WithCounts(adjusted[largest].Required - 1, 0);
                    else return false;
                }
            }

            private static void AddDoorMarks(List<ScreenMark> marks, DoorLayout door, string what)
            {
                marks.Add(new ScreenMark($"threshold of {what}", door.ThresholdLocal, TimeField.ThresholdRadius + TimeField.PlayerRadius));
                if (door.HasSwitch) marks.Add(new ScreenMark($"switch of {what}", door.SwitchLocal, TimeField.SwitchRadius));
            }

            /// <summary>The door with the first switch spot in the lane, on the far side, that is free and far enough.</summary>
            private static DoorLayout WithSwitch(DoorLayout door, ScreenContext ctx, List<ScreenMark> own, List<Rect> booths)
            {
                float sx = door.DoorLocal.x >= 0f ? 1f : -1f;
                float sy = door.DoorLocal.y >= 0f ? 1f : -1f;
                Vector2 threshold = door.ThresholdLocal;
                foreach (float x in SwitchLaneX)
                {
                    foreach (float y in new[] { sy * 0.6f, 0f, -sy * 0.6f })
                    {
                        var at = new Vector2(-sx * x, y);
                        if (!Free(at, TimeField.SwitchRadius, ctx.Others) || !Free(at, TimeField.SwitchRadius, own)) continue;
                        if (TimeField.WalkSeconds(at, threshold) < MinWalkSeconds) continue;
                        if (!PathClear(at, threshold, ctx.Blocked, booths)) continue;
                        return door.WithSwitch(at);
                    }
                }
                return door;
            }

            private static bool Free(Vector2 at, float r, List<ScreenMark> marks)
            {
                foreach (ScreenMark m in marks)
                    if (Vector2.Distance(at, m.Local) < Gap(r, m.Radius)) return false;
                return true;
            }

            private PhysicsTrialSpec TryTrial(ScreenAddress screen, List<TimedDoorSpec> specs, List<ScreenMark> own, SeededRng rng,
                out Dictionary<int, List<Vector2>> doorBoulders, out List<TimedDoorSpec> adjusted)
            {
                doorBoulders = null;
                adjusted = specs;
                // What the doors here get without a trial: the trial may not cost them boulders.
                FitBoulders(screen, specs, null, own, out List<TimedDoorSpec> baseline, out _, out _);
                ScreenContext ctx = Ctx(screen);
                var candidates = new List<(float bx, int side)>();
                for (float x = BoothOuter / 2f + ScreenModule.ExitLaneHalfWidth; x <= 6.5f - BoothOuter / 2f + 1e-4f; x += GridStep)
                    foreach (int side in new[] { 1, -1 })
                        foreach (float sign in new[] { 1f, -1f })
                        {
                            float bx = sign * x;
                            Rect rect = BoothFootprint(bx, side);
                            if (!BoothFits(rect, BoothDoor(bx, side, TrialRequired), ctx, own)) continue;
                            candidates.Add((bx, side));
                        }
                if (candidates.Count < TrialBooths) return null;

                for (int attempt = 0; attempt < TrialAttempts; attempt++)
                {
                    (float ax, int aside) = candidates[rng.NextInt(candidates.Count)];
                    Rect aRect = BoothFootprint(ax, aside);
                    DoorLayout aDoor = BoothDoor(ax, aside, TrialRequired);
                    var partners = new List<(float bx, int side)>();
                    foreach ((float bx, int side) c in candidates)
                    {
                        Rect r = BoothFootprint(c.bx, c.side);
                        if (Grow(aRect, 0.5f).Overlaps(r)) continue;
                        if (Vector2.Distance(aDoor.FieldLocal, BoothDoor(c.bx, c.side, TrialRequired).FieldLocal) < 2f * TimeField.FieldRadius + 0.5f) continue;
                        partners.Add(c);
                    }
                    if (partners.Count == 0) continue;
                    (float bx2, int bside) = partners[rng.NextInt(partners.Count)];
                    Rect bRect = BoothFootprint(bx2, bside);
                    var booths = new List<Rect> { aRect, bRect };
                    var marks = new List<ScreenMark>(own);
                    // Booths must not block any door's way from its switch.
                    bool blocksDoor = false;
                    foreach (TimedDoorSpec s in specs)
                        if (s.Screen == screen && s.Layout != null && s.Layout.HasSwitch &&
                            !PathClear(s.Layout.SwitchLocal, s.Layout.ThresholdLocal, ctx.Blocked, booths)) blocksDoor = true;
                    if (blocksDoor) continue;
                    marks.Add(new ScreenMark("trial booth", aRect.center, BoothOuter * 0.71f));
                    marks.Add(new ScreenMark("trial booth", bRect.center, BoothOuter * 0.71f));
                    DoorLayout a = WithSwitch(aDoor, ctx, marks, booths);
                    if (!a.HasSwitch) continue;
                    AddDoorMarks(marks, a, "trial booth");
                    DoorLayout b = WithSwitch(BoothDoor(bx2, bside, TrialRequired), ctx, marks, booths);
                    if (!b.HasSwitch) continue;
                    AddDoorMarks(marks, b, "trial booth");
                    var spec = new PhysicsTrialSpec(screen, new[] { new TrialBoothSpec(aRect, a), new TrialBoothSpec(bRect, b) },
                        null, TrialRequired);
                    if (!FitBoulders(screen, specs, spec, marks, out adjusted, out doorBoulders, out List<Vector2> trialBoulders)) continue;
                    bool costly = false;
                    for (int i = 0; i < adjusted.Count; i++)
                        if (adjusted[i].Required != baseline[i].Required || adjusted[i].Spare != baseline[i].Spare) costly = true;
                    if (costly) continue;
                    // Every door on this screen still has room near it with the booths there.
                    var grid = new ReachGrid(ctx.Blocked, booths);
                    bool roomy = true;
                    foreach (TimedDoorSpec s in adjusted)
                        if (s.Screen == screen && s.Layout != null && grid.Room(grid.ReachableTo(s.Layout), s.Layout) < s.Required) roomy = false;
                    foreach (TrialBoothSpec booth in spec.Booths)
                        if (grid.Room(grid.ReachableTo(booth.Layout), booth.Layout) < TrialRequired) roomy = false;
                    if (!roomy) continue;
                    return new PhysicsTrialSpec(screen, spec.Booths, trialBoulders, TrialRequired);
                }
                return null;
            }

            private static bool BoothFits(Rect rect, DoorLayout door, ScreenContext ctx, List<ScreenMark> own)
            {
                if (!ScreenModule.KeepsExitsOpen(rect)) return false;
                Rect grown = Grow(rect, 0.05f);
                foreach (Rect b in ctx.Blocked)
                    if (b.Overlaps(grown)) return false;
                foreach (ScreenMark m in ctx.Others)
                    if (DistanceToRect(m.Local, rect) < m.Radius + GapMargin) return false;
                foreach (ScreenMark m in own)
                    if (DistanceToRect(m.Local, rect) < m.Radius + GapMargin) return false;
                // The booth's threshold is in the lane: keep it off other interactables too.
                return Free(door.ThresholdLocal, TimeField.ThresholdRadius + TimeField.PlayerRadius, own);
            }

            /// <summary>
            /// Seeded boulder spots for every door on a screen (required + spare each) and, with a trial, its shared
            /// boulders: off the lanes, out of walls and booths, outside every time field, clear of interactables and
            /// apart, each reachable to its door (both booths for the trial's). False if any falls short.
            /// </summary>
            private bool PlaceBoulders(ScreenAddress screen, List<TimedDoorSpec> specs, PhysicsTrialSpec trial,
                List<ScreenMark> own, SeededRng rng, out Dictionary<int, List<Vector2>> doorBoulders, out List<Vector2> trialBoulders)
            {
                ScreenContext ctx = Ctx(screen);
                doorBoulders = new Dictionary<int, List<Vector2>>();
                trialBoulders = new List<Vector2>();
                var booths = new List<Rect>();
                var doorCentres = new List<Vector2>();
                if (trial != null)
                    foreach (TrialBoothSpec b in trial.Booths)
                    {
                        booths.Add(b.Footprint);
                        doorCentres.Add(b.Layout.FieldLocal);
                    }
                for (int i = 0; i < specs.Count; i++)
                    if (specs[i].Screen == screen && specs[i].Layout != null) doorCentres.Add(specs[i].Layout.FieldLocal);

                var grid = new ReachGrid(ctx.Blocked, booths);
                var candidates = new List<Vector2>();
                Vector2 half = Game.Tracer.ScreenMath.DefaultScreenSize / 2f;
                for (float y = -half.y; y <= half.y + 1e-4f; y += GridStep)
                {
                    for (float x = -half.x; x <= half.x + 1e-4f; x += GridStep)
                    {
                        var c = new Vector2(x, y);
                        if (!ScreenModule.KeepsExitsOpen(Square(c, BoulderHalf))) continue;
                        if (DiscHits(c, BoulderHalf, ctx.Blocked) || DiscHits(c, BoulderHalf, booths)) continue;
                        bool near = false;
                        foreach (Vector2 d in doorCentres)
                            if (Vector2.Distance(c, d) < TimeField.FieldRadius + FieldClearance) near = true;
                        if (near || !Free(c, Boulder.Radius, ctx.Others) || !Free(c, Boulder.Radius, own)) continue;
                        candidates.Add(c);
                    }
                }

                var chosen = new List<Vector2>();
                bool ok = true;
                List<Vector2> Pick(int count, List<bool[]> reaches)
                {
                    var free = new List<Vector2>();
                    foreach (Vector2 c in candidates)
                    {
                        bool spaced = true;
                        foreach (Vector2 b in chosen)
                            if (Vector2.Distance(b, c) < BoulderSpacing) spaced = false;
                        if (!spaced) continue;
                        bool reaches1 = true;
                        foreach (bool[] r in reaches)
                            if (!grid.Reaches(r, c)) reaches1 = false;
                        if (reaches1) free.Add(c);
                    }
                    var picked = new List<Vector2>();
                    while (picked.Count < count && free.Count > 0)
                    {
                        Vector2 p = free[rng.NextInt(free.Count)];
                        picked.Add(p);
                        chosen.Add(p);
                        free.RemoveAll(c => Vector2.Distance(c, p) < BoulderSpacing);
                    }
                    if (picked.Count < count) ok = false;
                    return picked;
                }

                for (int i = 0; i < specs.Count; i++)
                {
                    TimedDoorSpec s = specs[i];
                    if (s.Screen != screen || s.Layout == null) continue;
                    doorBoulders[i] = Pick(s.Required + s.Spare, new List<bool[]> { grid.ReachableTo(s.Layout) });
                }
                if (trial != null)
                {
                    var reaches = new List<bool[]>();
                    foreach (TrialBoothSpec b in trial.Booths) reaches.Add(grid.ReachableTo(b.Layout));
                    trialBoulders = Pick(trial.Required, reaches);
                }
                return ok;
            }

            /// <summary>
            /// Spots other faces' content uses on each screen (local): Biology buttons (required and optional),
            /// flowers and trial pieces, Chemistry lead plates and trial pieces, and open pickups (the isotope
            /// dispenser included).
            /// </summary>
            private static Dictionary<ScreenAddress, List<ScreenMark>> OtherFaceMarks(CubeModel model, ItemPlacement placement,
                Func<ScreenAddress, ScreenModule> moduleAt, IReadOnlyList<BeakKind> kinds)
            {
                var marks = new Dictionary<ScreenAddress, List<ScreenMark>>();
                void Add(ScreenAddress s, string what, Vector2 at, float r)
                {
                    if (!marks.TryGetValue(s, out var list)) marks[s] = list = new List<ScreenMark>();
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
                {
                    foreach (BeakGateSpec s in biology.Gates)
                    {
                        if (s.Kind == BeakGateKind.Button && SlotLocal(s.Screen, s.Slot) is Vector2 local)
                            Add(s.Screen, $"Biology button ({s})", ButtonGate.ButtonLocal(local), BeakButton.Radius);
                        if (s.Kind == BeakGateKind.FlowerVine)
                            Add(s.FlowerScreen, $"Biology flower ({s})", FlowerVineGate.FlowerLocal, PollinationFlower.Radius);
                    }
                    ScreenModule module = moduleAt(biology.TrialScreen);
                    if (module != null)
                    {
                        var rng = new SeededRng(unchecked((ulong)(uint)model.Seed), BiologyFace.PopulationRngStream);
                        List<Vector2> spots = BiologyTrial.PickSpots(BiologyTrial.LaneSideSpots(TownPlan.BlockedRects(module)),
                            BiologyTrial.PiecesFor(biology.Beak), rng);
                        foreach (Vector2 p in spots) Add(biology.TrialScreen, "Biology trial piece", p, BiologyTrial.PieceBoundRadius);
                    }
                }

                ChemistryPlan chemistry = ChemistryPlan.Create(model, placement);
                if (chemistry != null)
                {
                    foreach (IsotopeGateSpec s in chemistry.Gates)
                        if (s.Stage == IsotopeStage.Lead && SlotLocal(s.Screen, s.Slot) is Vector2 local)
                            Add(s.Screen, $"lead plate ({s})", LeadPlateGate.PlateLocal(local), LeadPlate.Radius);
                    ScreenModule module = moduleAt(chemistry.TrialScreen);
                    if (module != null)
                        foreach (Vector2 p in ChemistryPlan.TrialSpots(model.Seed, module))
                            Add(chemistry.TrialScreen, "Chemistry trial piece", p, BiologyTrial.PieceBoundRadius);
                }

                foreach (PickupPlacement p in placement.Pickups)
                    if (!p.IsGuarded) Add(p.Screen, $"{p.Item} pickup", CubeWorld.OpenPickupOffset, ItemPickup.Radius);
                return marks;
            }
        }
    }
}
