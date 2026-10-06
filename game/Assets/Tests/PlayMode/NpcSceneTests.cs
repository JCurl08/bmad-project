using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Cube.Tests
{
    /// <summary>
    /// NPCs in the real Cube scene: the F3 debug spawn puts one NPC of each race around the player with
    /// lines built from the run's facts (which match what the reveal later builds), talking to one opens
    /// its lines, and F4 toggles hostility for the nearest NPC's race.
    /// </summary>
    public class NpcSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private CubeDebug debug;

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
            debug = Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsNotNull(world);
            Assert.IsNotNull(navigator);
            Assert.IsNotNull(debug);
        }

        [UnityTest]
        public IEnumerator F3_SpawnsOneNpcPerRace_WithRunTrueLines_AndTalkingOpensThem()
        {
            yield return Load();
            RunFacts beforeReveal = RunFacts.Of(world);

            yield return this.Tap(keyboard.f3Key, holdFrames: 2);
            IReadOnlyList<NpcTalker> npcs = debug.DebugNpcs;
            Assert.AreEqual(RaceExtensions.All.Length, npcs.Count);
            CollectionAssert.AreEquivalent(RaceExtensions.All, npcs.Select(n => n.Race));
            foreach (NpcTalker npc in npcs)
            {
                List<string> expected = Dialogue.For(npc.Spec, beforeReveal, debug.HintDensity);
                CollectionAssert.AreEqual(expected, npc.Lines, npc.Spec.ToString());
                Assert.AreEqual(Dialogue.MaxLines, npc.Lines.Count, "A role line, then the hint");
                Assert.AreEqual(NpcFactory.ChooseParts(world.Seed, npc.Race, 0).Signature(), npc.Spec.Signature());
            }

            // Talk to one: bring it next to the player (the others out of the way) and press Interact.
            NpcTalker target = npcs[0];
            foreach (NpcTalker other in npcs)
                if (other != target) other.gameObject.SetActive(false);
            Vector2 at = (Vector2)navigator.transform.position + new Vector2(1f, 0f);
            target.GetComponent<Rigidbody2D>().position = at;
            target.transform.position = at;
            yield return new WaitForFixedUpdate();
            yield return null; // input state is only valid in dynamic update
            yield return this.Tap(keyboard.eKey, holdFrames: 2);
            DialogueBox box = DialogueBox.Shared;
            Assert.IsTrue(box.IsOpen, "Interact next to an NPC did not open its dialogue");
            Assert.AreSame(target, box.Owner);
            Assert.AreEqual(target.Lines[0], box.CurrentLine);
            for (int i = 1; i < target.Lines.Count; i++)
            {
                yield return this.Tap(keyboard.eKey, holdFrames: 2);
                Assert.AreEqual(target.Lines[i], box.CurrentLine);
            }
            yield return this.Tap(keyboard.eKey, holdFrames: 2);
            Assert.IsFalse(box.IsOpen);

            // The pre-reveal facts are the ones the reveal builds, so town hints stay true.
            ScreenAddress science = world.Model.AllScreens().First(s => CubeLayout.IsLaidOut(world.Model, s.Face));
            navigator.TeleportTo(science, Facing.East);
            Assert.IsTrue(world.ScienceRevealed);
            Assert.AreEqual(beforeReveal.Layout.Signature(), world.Layout.Signature());
            Assert.AreEqual(beforeReveal.Placement.Signature(), world.ItemPlacement.Signature());
        }

        [UnityTest]
        public IEnumerator F4_TogglesHostilityForTheNearestNpcsRace_AndARebuildClearsIt()
        {
            yield return Load();
            debug.SpawnNpcs();
            // Wander and flee legs move, so make one NPC clearly the nearest.
            NpcTalker nearest = debug.DebugNpcs[2];
            Vector2 at = (Vector2)navigator.transform.position + new Vector2(0.6f, 0f);
            nearest.GetComponent<Rigidbody2D>().position = at;
            nearest.transform.position = at;

            yield return this.Tap(keyboard.f4Key, holdFrames: 2);
            Assert.IsTrue(world.Relations.IsHostile(nearest.Race));
            Assert.IsTrue(nearest.IsHostile);
            Assert.AreEqual(1, RaceExtensions.All.Count(r => world.Relations.IsHostile(r)));

            yield return this.Tap(keyboard.f4Key, holdFrames: 2);
            Assert.IsFalse(world.Relations.IsHostile(nearest.Race));

            world.Relations.SetHostile(Race.Alien, true);
            world.Rebuild(world.Seed + 1);
            yield return null;
            Assert.IsFalse(world.Relations.IsHostile(Race.Alien), "A new run starts at peace");
            Assert.IsEmpty(debug.DebugNpcs, "A rebuild removes the debug NPCs");
        }
    }
}
