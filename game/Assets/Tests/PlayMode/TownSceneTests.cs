using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Tracer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Town hub in the real Cube scene: a run starts with one NPC of each face race plus a per-screen share of Townsfolk
    /// on the Town face, on the seed's spots (clear of colliders, lanes and the centre); the same seed gives
    /// the same town; every Town NPC talks (sparse, run-true hints; the greeter adds the welcome); F5 replaces
    /// the population without leftovers; walking off a Town edge still lands on the shuffled cube; and (4.5) every face's
    /// centre is its town, the busiest screen of its face, with its leader by the statue and no enemies, over seeds 1-50.
    /// </summary>
    public class TownSceneTests : InputTestFixture
    {
        /// <summary>Lengths and speeds below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private TownPopulation town;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            town = Object.FindAnyObjectByType<TownPopulation>();
            Assert.IsNotNull(world);
            Assert.IsNotNull(navigator);
            Assert.IsNotNull(town, "The Cube scene has a TownPopulation");
        }

        /// <summary>Waits until the population of the current seed is in place (a rebuild spawns on the next frame).</summary>
        private IEnumerator WaitForPopulation()
        {
            for (int i = 0; i < 10 && (town.Pending || town.PopulatedSeed != world.Seed); i++)
                yield return null;
            Assert.IsFalse(town.Pending);
            Assert.AreEqual(world.Seed, town.PopulatedSeed);
        }

        private List<TownSpot> ExpectedSpots(int seed) =>
            TownPlan.Spots(seed, world.FaceSize, TownPlan.Specs(seed, world.FaceSize).Count,
                cell => TownPlan.BlockedRects(world.Library.TownModule(cell, world.FaceSize)));

        [UnityTest]
        public IEnumerator RunStart_TownHasOneNpcPerFaceRace_PlusTownsfolk_OnTheSeedsClearSpots()
        {
            yield return Load();
            yield return WaitForPopulation();
            Assert.AreEqual(Theme.Town, world.Model.ThemeOf(navigator.Face));
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "The run starts on the Town start screen");

            IReadOnlyList<NpcTalker> npcs = town.Npcs;
            foreach (Race race in TownPlan.FaceRaces)
                Assert.AreEqual(1, npcs.Count(n => n.Race == race), $"Town has exactly one {race}");
            int townsfolk = npcs.Count(n => n.Race == Race.Townsfolk);
            Assert.That(townsfolk, Is.InRange(TownPlan.MinTownsfolk(world.FaceSize), TownPlan.MaxTownsfolk(world.FaceSize)));
            Assert.AreEqual(TownPlan.FaceRaces.Length + townsfolk, npcs.Count);
            CollectionAssert.AreEqual(TownPlan.Specs(world.Seed, world.FaceSize).Select(s => s.Signature()), npcs.Select(n => n.Spec.Signature()));
            CollectionAssert.AreEqual(ExpectedSpots(world.Seed), town.Spots, "Spawn spots are the seed's pure spots");

            for (int i = 0; i < npcs.Count; i++)
            {
                TownSpot spot = town.Spots[i];
                Assert.AreEqual(CubeModel.StartFace, spot.Screen.Face);
                Vector2 feet = world.ScreenCenter(spot.Screen) + spot.Local;
                // Nothing solid but this NPC at its spawn spot (the NPCs move, so check the spot, not where it is now).
                var size = new Vector2(NpcFactory.BodyWidth, NpcFactory.MaxBodyHeight);
                foreach (Collider2D hit in Physics2D.OverlapBoxAll(feet + new Vector2(0f, size.y / 2f), size, 0f))
                {
                    if (hit.isTrigger || !hit.enabled) continue;
                    NpcTalker owner = hit.GetComponentInParent<NpcTalker>();
                    Assert.IsTrue(owner != null, $"NPC {i} spawned on {hit.name}");
                }
            }
        }

        [UnityTest]
        public IEnumerator SameSeed_GivesTheSameTown()
        {
            yield return Load();
            world.Rebuild(1234);
            yield return WaitForPopulation();
            string[] first = town.Npcs.Select(n => n.Spec.Signature()).ToArray();
            TownSpot[] firstSpots = town.Spots.ToArray();

            world.Rebuild(77);
            yield return WaitForPopulation();
            CollectionAssert.AreNotEqual(firstSpots, town.Spots.ToArray(), "Another seed, another town");

            world.Rebuild(1234);
            yield return WaitForPopulation();
            CollectionAssert.AreEqual(first, town.Npcs.Select(n => n.Spec.Signature()));
            CollectionAssert.AreEqual(firstSpots, town.Spots);
            for (int i = 0; i < town.Npcs.Count; i++)
                Assert.AreEqual(world.ScreenCenter(town.Spots[i].Screen) + town.Spots[i].Local,
                    (Vector2)town.Npcs[i].transform.position, $"NPC {i} spawned where its spot says");
        }

        [UnityTest]
        public IEnumerator EveryTownNpc_Talks_WithSparseRunTrueHints_AndTheGreeterWelcomes()
        {
            yield return Load();
            yield return WaitForPopulation();
            RunFacts facts = RunFacts.Of(world);
            List<NpcTalker> npcs = town.Npcs.ToList();
            DialogueBox box = DialogueBox.Shared;

            for (int i = 0; i < npcs.Count; i++)
            {
                NpcTalker target = npcs[i];
                List<string> expected = Dialogue.For(target.Spec, facts, HintGenerator.FirstRunDensity);
                bool greeter = TownPlan.IsGreeter(target.Spec);
                Assert.AreEqual(i == 0, greeter, "The greeter is the first Town NPC");
                if (greeter) expected = TownSign.GreeterLines(expected);
                CollectionAssert.AreEqual(expected, target.Lines, target.Spec.ToString());
                Assert.AreEqual(Dialogue.MaxLines, target.Lines.Count, "A flavour line, then the hint");
                Assert.AreEqual(greeter ? TownSign.WelcomeLines[0] : Dialogue.RoleLine(target.Spec), target.Lines[0],
                    "The greeter welcomes; everyone else says their race- and role-flavoured line");
                List<Hint> hints = HintGenerator.For(target.Spec, facts, HintDensity.Sparse);
                Assert.AreEqual(HintGenerator.SparseCount, hints.Count);
                Assert.IsTrue(hints.All(h => !h.Precise), "First-run hints are sparse");
                Assert.AreEqual(hints[0].Text, target.Lines[target.Lines.Count - 1]);
                foreach (string line in target.Lines)
                    Assert.IsFalse(line.ToLowerInvariant().Contains(Dialogue.ForbiddenWord), line);

                // Only this NPC active, right next to the player; Interact opens it and walks through every line.
                foreach (NpcTalker other in npcs)
                    other.gameObject.SetActive(other == target);
                Vector2 at = (Vector2)navigator.transform.position + new Vector2(1f * K, 0f);
                target.GetComponent<Rigidbody2D>().position = at;
                target.transform.position = at;
                yield return new WaitForFixedUpdate();
                yield return null;
                yield return this.Tap(keyboard.eKey, holdFrames: 2);
                Assert.IsTrue(box.IsOpen, $"Interact did not open {target.Spec}");
                Assert.AreSame(target, box.Owner);
                Assert.AreEqual(target.Lines[0], box.CurrentLine);
                for (int line = 1; line < target.Lines.Count; line++)
                {
                    yield return this.Tap(keyboard.eKey, holdFrames: 2);
                    Assert.AreEqual(target.Lines[line], box.CurrentLine);
                }
                yield return this.Tap(keyboard.eKey, holdFrames: 2);
                Assert.IsFalse(box.IsOpen);
            }
            StringAssert.Contains("Partition", string.Join(" ", npcs[0].Lines));
            StringAssert.Contains(TownSign.LastMixedPlace, string.Join(" ", npcs[0].Lines));
        }

        [UnityTest]
        public IEnumerator F5_ReplacesThePopulation_WithNoLeftovers()
        {
            yield return Load();
            yield return WaitForPopulation();
            List<NpcTalker> old = town.Npcs.ToList();
            int oldSeed = world.Seed;

            yield return this.Tap(keyboard.f5Key, holdFrames: 2);
            Assert.AreNotEqual(oldSeed, world.Seed);
            yield return WaitForPopulation();
            yield return null;

            Assert.IsTrue(old.All(n => n == null), "Every old Town NPC is gone");
            CollectionAssert.AreEqual(TownPlan.Specs(world.Seed, world.FaceSize).Select(s => s.Signature()), town.Npcs.Select(n => n.Spec.Signature()));
            CollectionAssert.AreEqual(ExpectedSpots(world.Seed), town.Spots);
            NpcTalker[] all = Object.FindObjectsByType<NpcTalker>(FindObjectsSortMode.None);
            Assert.IsNotNull(town.Leader, "Mayor Muddleton stands in Town's town (4.5)");
            Assert.AreEqual(town.Npcs.Count + 1, all.Length, "No duplicates or leftovers in the scene");
            CollectionAssert.AreEquivalent(town.Npcs.Append(town.Leader), all);
        }

        [UnityTest]
        public IEnumerator EveryFacesCentre_IsItsTown_TheBusiestScreen_WithItsLeader_AndNoEnemies_OverSeeds1To50()
        {
            yield return Load();
            var squares = Object.FindAnyObjectByType<TownSquares>();
            Assert.IsNotNull(squares, "The Cube scene has TownSquares");
            Vector2Int centre = TownSquare.Cell(world.FaceSize);
            for (int seed = 1; seed <= 50; seed++)
            {
                world.Rebuild(seed);
                yield return WaitForPopulation();
                Assert.IsTrue(world.RevealScience());
                yield return null;
                CubeModel model = world.Model;

                var npcsPerScreen = new Dictionary<ScreenAddress, int>();
                foreach (NpcTalker npc in Object.FindObjectsByType<NpcTalker>(FindObjectsSortMode.None))
                    if (world.TryScreenAt(npc.transform.position, out ScreenAddress at))
                        npcsPerScreen[at] = npcsPerScreen.TryGetValue(at, out int c) ? c + 1 : 1;
                var enemyScreens = new List<ScreenAddress>();
                foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
                {
                    // Every NPC carries dormant enemy parts (HostileNpc): only real enemies count.
                    if (enemy.GetComponent<NpcTalker>() != null) continue;
                    if (world.TryScreenAt(enemy.transform.position, out ScreenAddress at)) enemyScreens.Add(at);
                }
                Assert.Greater(enemyScreens.Count, 0, $"seed {seed}: the faces still have their enemies");

                for (int f = 0; f < CubeSettings.FaceCount; f++)
                {
                    var face = (FaceId)f;
                    var townScreen = new ScreenAddress(face, centre);
                    string context = $"seed {seed} {face} ({model.ThemeOf(face)})";
                    ScreenModule module = world.ModuleAt(townScreen);
                    Assert.IsNotNull(module, context);
                    Assert.IsTrue(module.IsTownScreen, $"{context}: the centre is a town");
                    TownSquareView view = module.GetComponentInChildren<TownSquareView>();
                    Assert.IsNotNull(view, $"{context}: the town is built");
                    Assert.AreEqual(model.ThemeOf(face), view.Theme, $"{context}: in its race's style");
                    Assert.GreaterOrEqual(view.Buildings.Count, 3, $"{context}: buildings");
                    Assert.IsTrue(view.Buildings.All(b => b != null && b.enabled && !b.isTrigger), $"{context}: the buildings are solid");
                    Assert.IsNotNull(view.Statue, $"{context}: a statue");
                    Assert.IsNotNull(view.MarkedDoor, $"{context}: a door marked for 4.6");

                    // The leader stands near the statue and gives the quest hook.
                    NpcTalker leader = face == CubeModel.StartFace ? town.Leader : squares.LeaderOf(face);
                    Assert.IsNotNull(leader, $"{context}: a leader");
                    Assert.IsTrue(leader.Spec.IsLeader, context);
                    Assert.AreEqual(TownSquare.RaceOf(model.ThemeOf(face)), leader.Spec.Race, context);
                    Assert.IsTrue(world.TryScreenAt(leader.transform.position, out ScreenAddress leaderAt) && leaderAt == townScreen, $"{context}: the leader is in the town");
                    Assert.Less(Vector2.Distance(leader.transform.position, view.Statue.transform.position), TownLayout.LeaderReach + 1f, $"{context}: near the statue");
                    // Its quest names its face's fragments (4.8): the die number, none found yet.
                    CollectionAssert.AreEqual(Dialogue.QuestConversation(leader.Spec.Race, world.Dice.Of(face), 0), leader.Lines, context);
                    Assert.IsNotNull(leader.GetComponent<LeaderQuest>(), $"{context}: the leader carries the quest");

                    // The busiest screen of its face, and no enemy.
                    int inTown = npcsPerScreen.TryGetValue(townScreen, out int n) ? n : 0;
                    for (int y = 0; y < world.FaceSize; y++)
                        for (int x = 0; x < world.FaceSize; x++)
                        {
                            var other = new ScreenAddress(face, x, y);
                            if (other == townScreen) continue;
                            int there = npcsPerScreen.TryGetValue(other, out int m) ? m : 0;
                            Assert.Greater(inTown, there, $"{context}: {other} has {there} people, the town {inTown}");
                        }
                    Assert.IsFalse(enemyScreens.Contains(townScreen), $"{context}: an enemy in the town");

                    // Its doorways and lanes are clear of the town's solids.
                    Vector2 c0 = world.ScreenCenter(townScreen);
                    float lane = ScreenModule.ExitLaneHalfWidth - 0.05f;
                    Vector2 half = CubeWorld.ScreenSize / 2f;
                    foreach (Rect laneRect in new[] { Rect.MinMaxRect(-lane, -half.y, lane, half.y), Rect.MinMaxRect(-half.x, -lane, half.x, lane) })
                        foreach (Collider2D hit in Physics2D.OverlapBoxAll(c0 + laneRect.center, laneRect.size, 0f))
                            Assert.IsNull(hit.GetComponentInParent<TownSquareView>(), $"{context}: {hit.name} is on a lane");
                }
            }
        }

        [UnityTest]
        public IEnumerator WalkOffATownEdge_LandsOnTheShuffledCube()
        {
            yield return Load();
            yield return WaitForPopulation();
            world.UnlockAllEdges(); // 4.9: this walk is about the reveal, not the locks (LockSceneTests)
            CubeModel model = world.Model;
            Facing direction = new[] { Facing.East, Facing.North, Facing.West, Facing.South }
                .First(d => !model.IsSealed(CubeModel.NeighborFace(CubeModel.StartFace, d)));
            List<NpcTalker> beforeReveal = town.Npcs.ToList();
            string[] signatures = town.Specs.Select(s => s.Signature()).ToArray();
            TownSpot[] spotsBefore = town.Spots.ToArray();

            yield return this.HoldUntil(TestInput.KeyFor(keyboard, direction), () => navigator.Face != CubeModel.StartFace, 10f);
            yield return null;

            Assert.AreNotEqual(CubeModel.StartFace, navigator.Face, $"Walking {direction} never left Town (an NPC in the lane?)");
            Assert.AreEqual(CubeModel.NeighborFace(CubeModel.StartFace, direction), navigator.Face);
            Assert.IsTrue(world.ScienceRevealed, "Leaving town reveals the shuffled science faces");
            Assert.IsNotNull(world.ModuleAt(navigator.Current));
            Assert.AreEqual(model.ThemeOf(navigator.Face), world.ModuleAt(navigator.Current).Theme);

            // The reveal rebuilds the town: new objects, the same NPCs on the same spots, with post-reveal lines.
            yield return null;
            Assert.IsTrue(beforeReveal.All(n => n == null), "The reveal replaced the Town NPCs");
            CollectionAssert.AreEqual(signatures, town.Specs.Select(s => s.Signature()));
            CollectionAssert.AreEqual(signatures, town.Npcs.Select(n => n.Spec.Signature()));
            CollectionAssert.AreEqual(spotsBefore, town.Spots);
            RunFacts revealed = RunFacts.Of(world);
            for (int i = 0; i < town.Npcs.Count; i++)
            {
                List<string> expected = Dialogue.For(town.Npcs[i].Spec, revealed, HintGenerator.FirstRunDensity);
                if (TownPlan.IsGreeter(town.Npcs[i].Spec)) expected = TownSign.GreeterLines(expected);
                CollectionAssert.AreEqual(expected, town.Npcs[i].Lines);
            }
        }
    }
}
