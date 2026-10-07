using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Every Math island screen of seeds 1-50, walked for real (1.26): the island graph the sweep's model (DoorwayReach) says
    /// a doorway reaches is checked against the player's own walk, with the real keys, colliders and swing. For each screen:
    /// every doorway is landed on (a teleport to the doorway, then a walk onto its island), walked out of and back in by;
    /// without the gap-crosser* each doorway's whole-plank islands are walked to, plank centre by plank centre, and every
    /// broken span on the way stops the player; then with the gap-crosser* every island the model says the doorway reaches
    /// is walked to, each broken span mended by walking up to its gap and swinging at it. Any island the model claims but the
    /// walk cannot reach (or a span the walk crosses without the gap-crosser*) is reported with the seed, screen and spot.
    /// Afterwards every mended span shows Mended, the walked planks are lit, and each lone doorway has its lookout sign (1.26).
    /// Game time runs one physics step per frame (Time.captureDeltaTime) and the player slows only for the last step of a
    /// leg, so it stops on the spot it aims at.
    /// </summary>
    public class IslandWalkTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private static readonly Facing[] Sides = { Facing.North, Facing.East, Facing.South, Facing.West };

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private MathFace math;
        private Inventory inventory;
        private PlayerAttack attack;
        private PlayerMover mover;
        private Rigidbody2D body;
        private float baseSpeed;

        private readonly List<string> failures = new List<string>();
        private string where;
        private bool aborted;
        private int screens, doorways, islandsWalked, spansMended, spansBlocked, loneDoorways, planksLit, signs;

        /// <summary>The planks of the current screen the walker crossed, and the broken spans it swung at and mended.</summary>
        private readonly HashSet<int> crossed = new HashSet<int>(), swungAt = new HashSet<int>();

        private float savedCapture;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            savedCapture = Time.captureDeltaTime;
        }

        /// <summary>Puts back what the test changes (game time, the player's speed), however the test ended.</summary>
        public override void TearDown()
        {
            Time.captureDeltaTime = savedCapture;
            if (mover != null) mover.Speed = baseSpeed;
            base.TearDown();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            math = world.GetComponent<MathFace>();
            inventory = navigator.GetComponent<Inventory>();
            attack = navigator.GetComponent<PlayerAttack>();
            mover = navigator.GetComponent<PlayerMover>();
            body = navigator.GetComponent<Rigidbody2D>();
            baseSpeed = mover.Speed;
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private IEnumerator StartRunOntoMath(int seed)
        {
            world.Rebuild(seed);
            yield return null;
            foreach (IFaceContent content in world.FaceContents)
            {
                if (content is BiologyFace b) b.SpawnEnemies = false;
                if (content is ChemistryFace c) c.SpawnEnemies = false;
                if (content is PhysicsFace p) p.SpawnEnemies = false;
                if (content is EarthFace e) e.SpawnEnemies = false;
            }
            math.SpawnEnemies = false;
            world.RevealScience();
            Assert.IsNotNull(math.Plan, $"seed {seed}: the Math plan");
            yield return null;
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator EveryIslandScreen_Seeds1To50_EveryIslandTheModelClaims_IsWalkedTo_FromItsDoorways_SpansMendedBySwinging()
        {
            yield return Load();
            Time.captureDeltaTime = Time.fixedDeltaTime;
            try
            {
                for (int seed = 1; seed <= 50; seed++)
                {
                    yield return StartRunOntoMath(seed);
                    var reach = new DoorwayReach(world.Model, math.Plan.IslandAt);
                    HashSet<(ScreenAddress Screen, int Region)> everywhere = reach.Reachable(true);
                    foreach (IslandScreenSpec spec in math.Plan.IslandScreens)
                    {
                        IslandScreen screen = math.IslandScreens.First(s => s.Layout == spec.Layout);
                        for (int i = 0; i < spec.Layout.Islands.Count; i++)
                            if (!everywhere.Contains((spec.Screen, i)))
                                failures.Add($"seed {seed} {spec.Screen}: the model never reaches island {i}");
                        screens++;
                        aborted = false;
                        yield return WalkScreen(seed, spec, screen, reach);
                    }
                }
            }
            finally
            {
                Time.captureDeltaTime = savedCapture;
                mover.Speed = baseSpeed;
            }
            Debug.Log($"Island walk: {screens} island screens, {doorways} doorways, {islandsWalked} islands walked to, " +
                      $"{spansMended} broken spans mended by a swing, {spansBlocked} stopped the player without the gap-crosser*, " +
                      $"{loneDoorways} lone doorways with no plank, {signs} lookout signs, {planksLit} planks lit; {failures.Count} problems.");
            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.Greater(planksLit, 0, "Walked planks lit up");
            Assert.Greater(spansMended, 0, "Spans were mended");
        }

        private void Fail(string what)
        {
            failures.Add($"{where}: {what}");
            aborted = true;
        }

        /// <summary>The islands of the screen a doorway's island joins, by the model's own steps (DoorwayReach.Neighbours, this screen only).</summary>
        private static HashSet<int> Claimed(DoorwayReach reach, ScreenAddress screen, int from, bool gap)
        {
            var seen = new HashSet<int> { from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                foreach ((ScreenAddress s, int region) in reach.Neighbours(screen, at, gap))
                    if (s == screen && seen.Add(region)) queue.Enqueue(region);
            }
            return seen;
        }

        private IEnumerator WalkScreen(int seed, IslandScreenSpec spec, IslandScreen screen, DoorwayReach reach)
        {
            IslandLayout layout = spec.Layout;
            Vector2 centre = screen.Centre;
            var open = Sides.Where(s => world.Model.TryStep(spec.Screen, s, out _, out _)).ToList();
            ItemDefinition crosser = world.ItemFor(Theme.Math);
            var equipment = navigator.GetComponent<Equipment>();
            crossed.Clear();
            swungAt.Clear();

            foreach (bool gap in new[] { false, true })
            {
                if (gap)
                {
                    if (!inventory.Has(crosser)) inventory.Add(crosser);
                    equipment.Equip(crosser);
                }
                else equipment.Equip(null);
                var walked = new HashSet<int>();
                foreach (Facing side in open)
                {
                    where = $"seed {seed} {spec.Screen} {side} doorway{(gap ? " (gap-crosser*)" : "")}";
                    aborted = false;
                    int door = layout.DoorIsland(side);
                    if (door < 0)
                    {
                        Fail("the doorway has no island");
                        continue;
                    }
                    if (!gap) doorways++;
                    // Teleported to the doorway: just inside the wall, then onto the island's middle.
                    navigator.TeleportTo(spec.Screen, side.Opposite());
                    yield return PutPlayerAt(centre + IslandLayout.Arrival(side));
                    if (layout.IslandAt(body.position - centre) != door) Fail($"landed off the doorway's island at {body.position - centre}");
                    if (aborted) continue;
                    yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[door]), door, centre, layout, "onto the doorway's island");
                    if (aborted) continue;

                    HashSet<int> claimed = Claimed(reach, spec.Screen, door, gap);
                    if (!gap && claimed.Count == 1 && !layout.Bridges.Any(b => b.Joins(door))) loneDoorways++;
                    if (!walked.Contains(door))
                    {
                        var visited = new HashSet<int>();
                        yield return Explore(door, claimed, visited, gap, screen, centre, layout);
                        if (aborted) continue;
                        foreach (int i in claimed)
                            if (!visited.Contains(i)) Fail($"island {i} (spot {layout.Spots[i]}) is claimed but was never walked to");
                        walked.UnionWith(visited);
                        islandsWalked += visited.Count;
                    }
                    if (!gap && !aborted) yield return OutAndBackIn(spec.Screen, side, door, centre, layout);
                }
                if (gap)
                    for (int i = 0; i < layout.Islands.Count; i++)
                        if (!walked.Contains(i) && open.Any(s => layout.DoorIsland(s) >= 0))
                            failures.Add($"seed {seed} {spec.Screen}: island {i} (spot {layout.Spots[i]}) was walked to from no doorway");
            }
            // What the walk did shows (visual only): every mended span changed colour, the planks walked lit up, and each
            // lone doorway's island has its lookout sign.
            for (int k = 0; k < layout.Bridges.Count; k++)
            {
                PlankLight light = screen.Lights[k];
                // Only what the walker did: a span it mended shows Mended, a whole plank it crossed shows Walked.
                if (swungAt.Contains(k) && light != PlankLight.Mended)
                    failures.Add($"seed {seed} {spec.Screen}: broken plank {k} was mended but shows {light}");
                if (crossed.Contains(k) && !layout.Bridges[k].Broken && light != PlankLight.Walked)
                    failures.Add($"seed {seed} {spec.Screen}: whole plank {k} was crossed but shows {light}");
                if (crossed.Contains(k) && layout.Bridges[k].Broken && light != PlankLight.Mended)
                    failures.Add($"seed {seed} {spec.Screen}: broken plank {k} was crossed but shows {light}");
                if (light == PlankLight.Walked) planksLit++;
                if (light != PlankLight.Unlit && (!screen.Glow(k).enabled || screen.Glow(k).color != IslandScreen.GlowFor(light)))
                    failures.Add($"seed {seed} {spec.Screen}: plank {k} is {light} but its glow does not show it");
            }
            int loneSides = open.Count(s => layout.IsLone(layout.DoorIsland(s)));
            signs += screen.Signs.Count;
            if (screen.Signs.Count != loneSides) failures.Add($"seed {seed} {spec.Screen}: {loneSides} lone doorways but {screen.Signs.Count} signs");
            equipment.Equip(null);
        }

        /// <summary>Walks every claimed island from one, plank centre by plank centre, and back (a depth-first walk).</summary>
        private IEnumerator Explore(int at, HashSet<int> claimed, HashSet<int> visited, bool gap, IslandScreen screen, Vector2 centre,
            IslandLayout layout)
        {
            visited.Add(at);
            for (int k = 0; k < layout.Bridges.Count; k++)
            {
                if (aborted) yield break;
                IslandBridge b = layout.Bridges[k];
                if (!b.Joins(at)) continue;
                int other = b.Other(at);
                if (b.Broken && !gap)
                {
                    if (claimed.Contains(other)) continue;
                    // Without the gap-crosser* the gap stops the player.
                    yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[other]), -1, centre, layout, $"toward broken plank {k}");
                    if (layout.IslandAt(body.position - centre) == other) Fail($"crossed broken plank {k} to island {other} without the gap-crosser*");
                    else if (!AtGapEdge(b, at, centre, layout, $"stopped walking toward broken plank {k}")) yield break;
                    else spansBlocked++;
                    yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[at]), at, centre, layout, $"back from broken plank {k}");
                    continue;
                }
                if (visited.Contains(other) || !claimed.Contains(other)) continue;
                if (b.Broken)
                {
                    BrokenSpan span = screen.Gaps.First(g => g.Bridge == k);
                    if (!span.IsMended)
                    {
                        yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[other]), -1, centre, layout, $"up to the gap of plank {k}");
                        if (layout.IslandAt(body.position - centre) == other)
                        {
                            Fail($"walked over the unmended gap of plank {k}");
                            yield break;
                        }
                        if (!AtGapEdge(b, at, centre, layout, $"stopped walking up to the gap of plank {k}")) yield break;
                        yield return new WaitForSeconds(attack.Cooldown + 0.02f);
                        if (attack.TryAttack() < 0) Fail("the swing did not happen");
                        yield return null;
                        if (!span.IsMended)
                        {
                            Fail($"a swing of the gap-crosser* at {body.position - centre} (facing {mover.Facing}) did not mend plank {k}'s gap at {span.transform.position - (Vector3)centre}");
                            yield break;
                        }
                        spansMended++;
                        swungAt.Add(k);
                    }
                }
                yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[other]), other, centre, layout, $"over plank {k} to island {other}");
                if (aborted) yield break;
                crossed.Add(k);
                yield return Explore(other, claimed, visited, gap, screen, centre, layout);
                if (aborted) yield break;
                yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[at]), at, centre, layout, $"back over plank {k} to island {at}");
            }
        }

        /// <summary>
        /// True when the player stands at the near edge of a broken plank's gap, walking from island from: its centre one
        /// player radius (give or take the contact offset) short of the gap. A stop anywhere earlier fails (with where it stood).
        /// </summary>
        private bool AtGapEdge(IslandBridge b, int from, Vector2 centre, IslandLayout layout, string what)
        {
            Vector2 axis = ((Vector2)(layout.Spots[b.Other(from)] - layout.Spots[from])).normalized;
            Rect g = b.Gap;
            float near = axis.x > 0.5f ? g.xMin : axis.x < -0.5f ? -g.xMax : axis.y > 0.5f ? g.yMin : -g.yMax;
            float short_ = near - Vector2.Dot(body.position - centre, axis);
            if (Mathf.Abs(short_ - TimeField.PlayerRadius) <= 0.1f) return true;
            Fail($"{what} {short_:0.00} short of the gap at {body.position - centre} (expected {TimeField.PlayerRadius}: against it)");
            return false;
        }

        /// <summary>Walks out of the doorway onto the next screen, then presses back and walks in onto the doorway's island again.</summary>
        private IEnumerator OutAndBackIn(ScreenAddress screen, Facing side, int door, Vector2 centre, IslandLayout layout)
        {
            world.Model.TryStep(screen, side, out ScreenAddress next, out _);
            bool faceEdge = ScreenEdges.IsFaceEdge(world.FaceSize, screen.Cell, side);
            yield return HoldUntil(side, () => navigator.Current != screen, 4f);
            if (navigator.Current != next)
            {
                Fail($"walking out reached {navigator.Current} (at {body.position - centre}), not {next}");
                yield break;
            }
            Facing back = faceEdge ? navigator.Facing.Opposite() : side.Opposite();
            yield return HoldUntil(back, () => navigator.Current == screen, 4f);
            if (navigator.Current != screen)
            {
                Fail($"pressing {back} from {next} did not come back (at {navigator.Current})");
                yield break;
            }
            yield return WalkTo(centre + IslandLayout.SpotCentre(layout.Spots[door]), door, centre, layout, "back in onto the doorway's island");
        }

        private IEnumerator PutPlayerAt(Vector2 at)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private IEnumerator HoldUntil(Facing toward, System.Func<bool> condition, float gameSeconds)
        {
            yield return null;
            mover.Speed = baseSpeed;
            Press(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return WaitUntilOrGameTimeout(condition, gameSeconds);
            Release(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Walks with one key straight along x or y toward a point (world) until it is reached or the player stops moving
        /// (a gap, the water). The speed drops for the last step so the player stops on the point. With expect >= 0 the walk
        /// must end on that island, within reach of the point.
        /// </summary>
        private IEnumerator WalkTo(Vector2 target, int expect, Vector2 centre, IslandLayout layout, string leg)
        {
            Vector2 d = target - body.position;
            Facing toward = Mathf.Abs(d.x) >= Mathf.Abs(d.y)
                ? (d.x > 0f ? Facing.East : Facing.West)
                : (d.y > 0f ? Facing.North : Facing.South);
            Vector2 axis = toward.ToVector();
            float step = Time.fixedDeltaTime;
            yield return null;
            mover.Speed = baseSpeed;
            Press(KeyFor(keyboard, toward), queueEventOnly: true);
            float end = Time.time + d.magnitude / baseSpeed + 1.5f;
            Vector2 last = body.position;
            int still = 0;
            while (Time.time < end)
            {
                yield return null;
                float remaining = Vector2.Dot(target - body.position, axis);
                if (remaining < 0.02f) break;
                mover.Speed = Mathf.Clamp(remaining / step, 0.2f, baseSpeed);
                still = (body.position - last).sqrMagnitude < 1e-6f ? still + 1 : 0;
                last = body.position;
                if (still > 8) break;
            }
            mover.Speed = 0f;
            Release(KeyFor(keyboard, toward), queueEventOnly: true);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
            mover.Speed = baseSpeed;
            if (expect < 0) yield break;
            Vector2 local = body.position - centre;
            if (layout.IslandAt(local) != expect || (body.position - target).magnitude > 0.3f)
                Fail($"walking {leg} stopped at {local} (island {layout.IslandAt(local)}), short of {target - centre} on island {expect}");
        }
    }
}
