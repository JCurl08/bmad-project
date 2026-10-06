using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The Town hub in the real Cube scene: a run starts with one NPC of each face race plus 2-4 Townsfolk
    /// on the Town face, on the seed's spots (clear of colliders, lanes and the centre); the same seed gives
    /// the same town; every Town NPC talks (sparse, run-true hints; the greeter adds the welcome); F5 replaces
    /// the population without leftovers; and walking off a Town edge still lands on the shuffled cube.
    /// </summary>
    public class TownSceneTests : InputTestFixture
    {
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

        private IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
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
            TownPlan.Spots(seed, world.FaceSize, TownPlan.Specs(seed).Count,
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
            Assert.That(townsfolk, Is.InRange(TownPlan.MinTownsfolk, TownPlan.MaxTownsfolk));
            Assert.AreEqual(5 + townsfolk, npcs.Count);
            CollectionAssert.AreEqual(TownPlan.Specs(world.Seed).Select(s => s.Signature()), npcs.Select(n => n.Spec.Signature()));
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
                Assert.AreEqual(Dialogue.Greeting(target.Spec), target.Lines[0]);
                Assert.AreEqual(Dialogue.RoleLine(target.Spec), target.Lines[greeter ? 1 + TownSign.WelcomeLines.Count : 1],
                    "Race- and role-flavoured line");
                List<Hint> hints = HintGenerator.Generate(facts, target.Spec.HintKind, target.Spec.Salt, HintDensity.Sparse);
                Assert.AreEqual(HintGenerator.SparseCount, hints.Count);
                Assert.IsTrue(hints.All(h => !h.Precise), "First-run hints are sparse");
                Assert.AreEqual(hints[0].Text, target.Lines[target.Lines.Count - 1]);
                foreach (string line in target.Lines)
                    Assert.IsFalse(line.ToLowerInvariant().Contains(Dialogue.ForbiddenWord), line);

                // Only this NPC active, right next to the player; Interact opens it and walks through every line.
                foreach (NpcTalker other in npcs)
                    other.gameObject.SetActive(other == target);
                Vector2 at = (Vector2)navigator.transform.position + new Vector2(1f, 0f);
                target.GetComponent<Rigidbody2D>().position = at;
                target.transform.position = at;
                yield return new WaitForFixedUpdate();
                yield return null;
                yield return Tap(keyboard.eKey);
                Assert.IsTrue(box.IsOpen, $"Interact did not open {target.Spec}");
                Assert.AreSame(target, box.Owner);
                Assert.AreEqual(target.Lines[0], box.CurrentLine);
                for (int line = 1; line < target.Lines.Count; line++)
                {
                    yield return Tap(keyboard.eKey);
                    Assert.AreEqual(target.Lines[line], box.CurrentLine);
                }
                yield return Tap(keyboard.eKey);
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

            yield return Tap(keyboard.f5Key);
            Assert.AreNotEqual(oldSeed, world.Seed);
            yield return WaitForPopulation();
            yield return null;

            Assert.IsTrue(old.All(n => n == null), "Every old Town NPC is gone");
            CollectionAssert.AreEqual(TownPlan.Specs(world.Seed).Select(s => s.Signature()), town.Npcs.Select(n => n.Spec.Signature()));
            CollectionAssert.AreEqual(ExpectedSpots(world.Seed), town.Spots);
            NpcTalker[] all = Object.FindObjectsByType<NpcTalker>(FindObjectsSortMode.None);
            Assert.AreEqual(town.Npcs.Count, all.Length, "No duplicates or leftovers in the scene");
            CollectionAssert.AreEquivalent(town.Npcs, all);
        }

        [UnityTest]
        public IEnumerator WalkOffATownEdge_LandsOnTheShuffledCube()
        {
            yield return Load();
            yield return WaitForPopulation();
            CubeModel model = world.Model;
            Facing direction = new[] { Facing.East, Facing.North, Facing.West, Facing.South }
                .First(d => !model.IsSealed(CubeModel.NeighborFace(CubeModel.StartFace, d)));
            List<NpcTalker> beforeReveal = town.Npcs.ToList();
            string[] signatures = town.Specs.Select(s => s.Signature()).ToArray();
            TownSpot[] spotsBefore = town.Spots.ToArray();
            KeyControl key = direction == Facing.East ? keyboard.dKey
                : direction == Facing.North ? keyboard.wKey
                : direction == Facing.West ? keyboard.aKey : keyboard.sKey;

            Press(key, queueEventOnly: true);
            float end = Time.realtimeSinceStartup + 10f;
            while (navigator.Face == CubeModel.StartFace && Time.realtimeSinceStartup < end)
                yield return null;
            Release(key, queueEventOnly: true);
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
            RunFacts revealed = new RunFacts(model, world.Layout, world.ItemPlacement);
            for (int i = 0; i < town.Npcs.Count; i++)
            {
                List<string> expected = Dialogue.For(town.Npcs[i].Spec, revealed, HintGenerator.FirstRunDensity);
                if (TownPlan.IsGreeter(town.Npcs[i].Spec)) expected = TownSign.GreeterLines(expected);
                CollectionAssert.AreEqual(expected, town.Npcs[i].Lines);
            }
        }
    }
}
