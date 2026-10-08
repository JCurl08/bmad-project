using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Tracer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Game.Cube.Tests.TestInput;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Runtime play against the real Cube scene (created by Cube > Create Cube Scene): walking off a
    /// Town edge lands where CubeModel predicts, walking toward a sealed face is blocked, and the
    /// F5 / F6 debug commands work.
    /// </summary>
    public class CubeSceneTests : InputTestFixture
    {
        /// <summary>Lengths and speeds below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

        private const string SceneName = "Cube";
        private const float WalkTimeout = 10f;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private ScreenCamera screenCamera;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator LoadCubeScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null; // (not WaitForFixedUpdate: input state is only valid in dynamic update)

            world = UnityEngine.Object.FindAnyObjectByType<CubeWorld>();
            navigator = UnityEngine.Object.FindAnyObjectByType<CubeNavigator>();
            screenCamera = UnityEngine.Object.FindAnyObjectByType<ScreenCamera>();
            Assert.IsNotNull(world, "CubeWorld not found in the Cube scene");
            Assert.IsNotNull(navigator, "CubeNavigator not found in the Cube scene");
            Assert.IsNotNull(screenCamera, "ScreenCamera not found in the Cube scene");
            Assert.IsNotNull(world.Model, "CubeWorld did not build its model");
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "Player should start on the Town start screen");
            Assert.AreEqual(Theme.Town, world.Model.ThemeOf(navigator.Face));
        }

        /// <summary>The screen a straight walk from the start screen leaves the face from.</summary>
        private ScreenAddress ExitScreen(Facing direction)
        {
            ScreenAddress here = world.Model.StartScreen;
            while (world.Model.IsInside(here.Cell + direction.ToVector()))
                here = new ScreenAddress(here.Face, here.Cell + direction.ToVector());
            return here;
        }


        [UnityTest]
        public IEnumerator WalkOffTownEdge_ArrivesOnAdjacentFaceAsModelPredicts()
        {
            yield return LoadCubeScene();
            CubeModel model = world.Model;

            // East first (the most natural walk), otherwise any direction into an unsealed face.
            Facing direction = new[] { Facing.East, Facing.North, Facing.West, Facing.South }
                .First(d => !model.IsSealed(CubeModel.NeighborFace(CubeModel.StartFace, d)));
            ScreenAddress exit = ExitScreen(direction);
            Assert.IsTrue(model.TryStep(exit, direction, out ScreenAddress expected, out Facing expectedFacing));

            // Every screen that belongs to a face; the camera must never show anything else (e.g. the gap).
            var faceScreens = new HashSet<Vector2Int>(
                model.AllScreens().Select(s => ScreenMath.ScreenIndex(world.ScreenCenter(s))));
            string offFace = null;
            void CheckCamera(string phase)
            {
                if (offFace == null && !faceScreens.Contains(screenCamera.CurrentScreen))
                    offFace = $"{phase}: camera on screen {screenCamera.CurrentScreen}, player at {navigator.transform.position}";
            }

            Assert.IsFalse(world.ScienceRevealed, "Science faces must stay unrevealed until leaving town");
            ScreenAddress arrived = default;
            bool crossed = false;
            Press(KeyFor(keyboard, direction), queueEventOnly: true);
            yield return WaitUntilOrTimeout(() =>
            {
                CheckCamera("before crossing");
                if (navigator.Face == CubeModel.StartFace) return false;
                crossed = true;
                arrived = navigator.Current;
                return true;
            }, WalkTimeout);
            // Keep walking a little after the crossing and keep watching the camera.
            float keepWalkingUntil = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < keepWalkingUntil)
            {
                CheckCamera("after crossing");
                yield return null;
            }
            Release(KeyFor(keyboard, direction), queueEventOnly: true);
            yield return null;
            yield return null;

            Assert.IsTrue(crossed, $"Player never left Town walking {direction}");
            Assert.IsNull(offFace, "Camera showed a screen outside every face");
            Assert.AreEqual(expected, arrived, $"Walking {direction} from {exit}");
            Assert.AreEqual(expectedFacing, navigator.Facing);
            Assert.AreEqual(1, navigator.Crossings);

            Vector2 local = (Vector2)navigator.transform.position - world.FaceOrigin(expected.Face);
            Assert.That(local.x, Is.InRange(0f, world.FaceExtent.x), "Player x outside the new face");
            Assert.That(local.y, Is.InRange(0f, world.FaceExtent.y), "Player y outside the new face");
            Assert.AreEqual(ScreenMath.ScreenIndex(navigator.transform.position), screenCamera.CurrentScreen,
                "Camera did not follow the player onto the new face");

            // Leaving town revealed the science layout, generated from the run seed alone.
            Assert.IsTrue(world.ScienceRevealed, "Crossing off Town did not reveal the science faces");
            Assert.IsNotNull(world.Layout);
            Assert.AreEqual(CubeLayout.Generate(new CubeModel(world.Seed), world.Library).Signature(),
                world.Layout.Signature(), "Revealed layout differs from the seed's layout");
            ScreenModule landedOn = world.ModuleAt(arrived);
            Assert.IsNotNull(landedOn, $"Landed on {arrived}, which has no module");
            Assert.AreEqual(model.ThemeOf(arrived.Face), landedOn.Theme);
            AssertLaidOut(model);
        }

        /// <summary>Turns off every face's enemy spawns (before a reveal).</summary>
        private void NoEnemies()
        {
            foreach (IFaceContent content in world.FaceContents)
            {
                if (content is BiologyFace b) b.SpawnEnemies = false;
                if (content is ChemistryFace c) c.SpawnEnemies = false;
                if (content is PhysicsFace p) p.SpawnEnemies = false;
                if (content is EarthFace e) e.SpawnEnemies = false;
                if (content is MathFace m) m.SpawnEnemies = false;
            }
        }

        /// <summary>Puts the player at a point (local to a screen's centre) on that screen, still, with nothing held.</summary>
        private IEnumerator PlaceOn(ScreenAddress screen, Vector2 local, Facing facing)
        {
            var body = navigator.GetComponent<Rigidbody2D>();
            navigator.TeleportTo(screen, facing);
            Vector2 at = world.ScreenCenter(screen) + local;
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>
        /// Two-way connections through the face gates (1.25): from every edge screen of every face, walking off the edge
        /// through its doorway with a key held crosses onto the next face and stops there, facing in, however long the key
        /// stays held (no walking on, no bounce back over a turned edge). After letting go, a fresh press toward the doorway
        /// behind (the reverse of the entry facing) walks back over the same edge to the screen the walk started on.
        /// </summary>
        [UnityTest, Timeout(600000)]
        public IEnumerator EveryFaceEdge_CrossStopsTheWalk_AndAFreshPressBackReturnsToTheSameScreen()
        {
            yield return LoadCubeScene();
            NoEnemies();
            world.RevealScience();
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
            var body = navigator.GetComponent<Rigidbody2D>();
            var mover = navigator.GetComponent<PlayerMover>();
            CubeModel model = world.Model;
            int n = model.FaceSize;
            var failures = new List<string>();
            int tried = 0;
            foreach (FaceId face in Enum.GetValues(typeof(FaceId)))
            {
                foreach (Facing direction in new[] { Facing.North, Facing.East, Facing.South, Facing.West })
                {
                    for (int i = 0; i < n; i++)
                    {
                        Vector2Int cell = direction == Facing.East ? new Vector2Int(n - 1, i)
                            : direction == Facing.West ? new Vector2Int(0, i)
                            : direction == Facing.North ? new Vector2Int(i, n - 1) : new Vector2Int(i, 0);
                        var from = new ScreenAddress(face, cell);
                        if (!model.TryStep(from, direction, out ScreenAddress expected, out Facing entry)) continue;

                        // Start in the doorway's lane, a little inside the edge.
                        Vector2 half = CubeWorld.ScreenSize / 2f;
                        yield return PlaceOn(from, Vector2.Scale(direction.ToVector(), half - Vector2.one * 1.2f * K), direction);

                        int crossings = navigator.Crossings;
                        Press(KeyFor(keyboard, direction), queueEventOnly: true);
                        yield return WaitUntilOrTimeout(() => navigator.Crossings > crossings, 3f);
                        tried++;
                        ScreenAddress arrived = navigator.Current;
                        Vector2 arrivedAt = body.position;
                        int afterCrossing = navigator.Crossings;
                        // Keep the key held: the player stays where it arrived.
                        float holdUntil = Time.realtimeSinceStartup + 0.3f;
                        float drift = 0f;
                        while (Time.realtimeSinceStartup < holdUntil)
                        {
                            yield return null;
                            drift = Mathf.Max(drift, Vector2.Distance(body.position, arrivedAt));
                        }
                        bool waiting = mover.WaitingForFreshPress;
                        Release(KeyFor(keyboard, direction), queueEventOnly: true);
                        yield return null;
                        yield return new WaitForFixedUpdate();
                        yield return null;
                        if (afterCrossing == crossings)
                        {
                            failures.Add($"{from} {direction}: never crossed (stuck at {body.position})");
                            continue;
                        }
                        if (arrived != expected) failures.Add($"{from} {direction}: arrived on {arrived}, model says {expected}");
                        if (navigator.Facing != entry) failures.Add($"{from} {direction}: arrived facing {navigator.Facing}, model says {entry}");
                        if (mover.Facing != (Vector2)entry.ToVector()) failures.Add($"{from} {direction}: the player does not face into the new face");
                        if (navigator.Crossings != afterCrossing || navigator.Face != expected.Face)
                        {
                            failures.Add($"{from} {direction} -> {arrived}: holding the key crossed again (now {navigator.Current})");
                            continue;
                        }
                        if (drift > 0.05f * K || !waiting)
                            failures.Add($"{from} {direction} -> {arrived}: the held key moved the player {drift:0.00} (waiting {waiting})");

                        // A fresh press toward the doorway behind walks back over the same edge.
                        crossings = navigator.Crossings;
                        Facing back = entry.Opposite();
                        yield return this.HoldUntil(KeyFor(keyboard, back), () => navigator.Crossings > crossings, 3f);
                        yield return null;
                        if (navigator.Crossings == crossings)
                            failures.Add($"{from} {direction} -> {arrived}: walking back {back} never crossed (stuck at {body.position})");
                        else if (navigator.Current != from)
                            failures.Add($"{from} {direction} -> {arrived}, back {back} -> {navigator.Current}");
                        else if (navigator.Facing != direction.Opposite())
                            failures.Add($"{from} {direction} -> {arrived}, back: facing {navigator.Facing}, expected {direction.Opposite()}");
                    }
                }
            }
            Assert.AreEqual(6 * 4 * n, tried, "Every edge screen of every face was tried");
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }

        /// <summary>
        /// Doorway edges (1.25): walking into a screen's edge away from its doorway is blocked by the wall, on a face edge
        /// and within a face; walking through the doorway leads on (smoothly within a face, no stop).
        /// </summary>
        [UnityTest]
        public IEnumerator ScreenEdges_AreWallsExceptAtTheCentredDoorway()
        {
            yield return LoadCubeScene();
            NoEnemies();
            world.RevealScience();
            var body = navigator.GetComponent<Rigidbody2D>();
            var mover = navigator.GetComponent<PlayerMover>();
            int n = world.Model.FaceSize;
            Vector2 half = CubeWorld.ScreenSize / 2f;
            // Inside the edge band (clear of every module obstacle), off the doorway, then in it.
            float bandX = half.x - ScreenModule.EdgeClearance + 0.05f, bandY = half.y - ScreenModule.EdgeClearance + 0.05f;
            // How far along the edge the probe stands, off the doorway: 2.6 tiles (scaled) up a side wall; on the town (4.5),
            // whose buildings stand by its side walls from 2 to 4.8 up, the clear strip above them (5.6 up).
            const float townAlong = 5.6f;
            var cases = new List<(ScreenAddress screen, Facing side, float along)>
            {
                (new ScreenAddress(CubeModel.StartFace, 1, n - 1), Facing.North, 4.5f * K), // a face edge
                (new ScreenAddress(CubeModel.StartFace, 1, 1), Facing.North, 4.5f * K), // within the face (the town)
                (new ScreenAddress(CubeModel.StartFace, n - 1, 1), Facing.East, 2.6f * K), // a face edge
                (new ScreenAddress(CubeModel.StartFace, n - 1, 1), Facing.West, 2.6f * K), // within the face
                (new ScreenAddress(CubeModel.StartFace, 1, 1), Facing.West, townAlong), // within the face: the town's west wall
                (new ScreenAddress(CubeModel.StartFace, 1, 1), Facing.East, townAlong), // within the face: the town's east wall
            };
            foreach (var (screen, side, along) in cases)
            {
                bool faceEdge = ScreenEdges.IsFaceEdge(n, screen.Cell, side);
                Vector2 off = side.IsHorizontal() ? new Vector2(side.ToVector().x * bandX, along) : new Vector2(along, side.ToVector().y * bandY);
                yield return PlaceOn(screen, off, side);
                int crossings = navigator.Crossings;
                yield return this.HoldUntil(KeyFor(keyboard, side), () => false, 0.8f);
                yield return null;
                Vector2 local = body.position - world.ScreenCenter(screen);
                Assert.AreEqual(crossings, navigator.Crossings, $"{screen} {side}: crossed through the wall");
                Assert.AreEqual(screen, navigator.Current, $"{screen} {side}: left the screen through the wall");
                float reach = Vector2.Dot(local, side.ToVector());
                float wallFace = (side.IsHorizontal() ? half.x : half.y) - ScreenEdges.EdgeWallThickness;
                Assert.LessOrEqual(reach, wallFace - 0.35f * K, $"{screen} {side}: the player stands against the wall, not in it");
                Assert.GreaterOrEqual(reach, wallFace - 0.45f * K, $"{screen} {side}: the player walked up to the wall");

                // Through the doorway: it leads on.
                Vector2 door = side.IsHorizontal() ? new Vector2(side.ToVector().x * bandX, 0f) : new Vector2(0f, side.ToVector().y * bandY);
                yield return PlaceOn(screen, door, side);
                Model().TryStep(screen, side, out ScreenAddress next, out _);
                yield return this.HoldUntil(KeyFor(keyboard, side), () => navigator.Current == next, 3f);
                yield return null;
                Assert.AreEqual(next, navigator.Current, $"{screen} {side}: the doorway did not lead on");
                Assert.AreEqual(faceEdge ? crossings + 1 : crossings, navigator.Crossings);
                Assert.AreEqual(faceEdge, mover.WaitingForFreshPress, $"{screen} {side}: only a face edge stops the walk");
            }
        }

        private CubeModel Model() => world.Model;

        /// <summary>Every built science screen has a module of its theme; one active core entrance per face; sealed faces are empty.</summary>
        private void AssertLaidOut(CubeModel model)
        {
            foreach (FaceId face in Enum.GetValues(typeof(FaceId)))
            {
                var screens = model.AllScreens().Where(s => s.Face == face).ToList();
                if (model.IsSealed(face))
                {
                    Assert.IsTrue(screens.All(s => world.ModuleAt(s) == null), $"Sealed {face} has modules");
                    continue;
                }
                Assert.IsTrue(screens.All(s => world.ModuleAt(s) != null && world.ModuleAt(s).Theme == model.ThemeOf(face)),
                    $"{face} is not fully laid out with {model.ThemeOf(face)} modules");
                foreach (ScreenAddress s in screens)
                {
                    foreach (ExitSlot exit in world.ModuleAt(s).Exits)
                    {
                        bool open = model.TryStep(s, exit.Facing, out _, out _);
                        Assert.AreEqual(open, exit.gameObject.activeSelf,
                            $"{s} {exit.Label}: active exit slots must not face a sealed face, open ones must stay active");
                    }
                }
                if (face == CubeModel.StartFace) continue;
                int activeCores = screens.Sum(s => world.ModuleAt(s).ActiveCoreEntrances().Count);
                Assert.AreEqual(1, activeCores, $"{face} ({model.ThemeOf(face)}) active core entrances");
            }
        }

        [UnityTest]
        public IEnumerator Player_IsDrawnAThirdLargerOnTheCanvas_ThanOnTheFirstScreens_ItsColliderUnchanged()
        {
            // 4.1 review: the hero (one tile on the first 16x10 screens, at 60 px per unit) reads 1.33x larger on the
            // 24x15 canvas (40 px per unit); visual only, the collider only scales with the screen.
            yield return LoadCubeScene();
            SpriteRenderer art = navigator.transform.Find("Visual").GetComponent<SpriteRenderer>();
            float canvasPixels = art.bounds.size.y * 600f / CubeWorld.ScreenSize.y;
            Assert.AreEqual(CubeSettings.PeopleArtScale, canvasPixels / (1f * 600f / 10f), 0.01f, "The hero's drawn height on the canvas");
            Assert.AreEqual(0.4f * K, navigator.GetComponent<CircleCollider2D>().radius, 1e-4f, "The collider is not grown with the art");
            Assert.Greater(art.sortingOrder, -4, "The hero draws over walls and gate arches");
        }

        [UnityTest]
        public IEnumerator RunStart_OnlyTownIsLaidOut_ScienceUnrevealed()
        {
            yield return LoadCubeScene();
            CubeModel model = world.Model;
            Assert.IsNotNull(world.Library, "CubeWorld has no ModuleLibrary wired in");
            Assert.IsFalse(world.ScienceRevealed);
            Assert.IsNull(world.Layout);
            foreach (ScreenAddress screen in model.AllScreens())
            {
                ScreenModule module = world.ModuleAt(screen);
                if (screen.Face == CubeModel.StartFace)
                {
                    Assert.IsNotNull(module, $"Town screen {screen} has no module");
                    Assert.AreEqual(Theme.Town, module.Theme);
                    Assert.AreEqual(world.Library.TownModule(screen.Cell, model.FaceSize).name,
                        module.name.Split(new[] { " @ " }, StringSplitOptions.None)[0], $"Town module on {screen} is not the fixed one");
                }
                else
                {
                    Assert.IsNull(module, $"{screen} was laid out before leaving town");
                }
            }
            // Town is N x N distinct screens: one module per cell, none repeated.
            List<ScreenModule> townModules = model.AllScreens().Where(s => s.Face == CubeModel.StartFace).Select(s => world.ModuleAt(s)).ToList();
            Assert.AreEqual(model.FaceSize * model.FaceSize, townModules.Count);
            foreach (ScreenAddress cell in model.AllScreens().Where(s => s.Face == CubeModel.StartFace))
                Assert.IsNotNull(world.ModuleAt(cell), $"Town cell {cell} has a module");
            Assert.AreEqual(townModules.Count, townModules.Select(m => m.name.Split(new[] { " @ " }, StringSplitOptions.None)[0]).Distinct().Count(),
                "Every Town screen shows its own module");

            // Arriving on a built science face by debug teleport also reveals; a second reveal is a no-op.
            ScreenAddress science = model.AllScreens().First(s => CubeLayout.IsLaidOut(model, s.Face));
            navigator.TeleportTo(science, Facing.East);
            Assert.IsTrue(world.ScienceRevealed, "Teleporting onto a science face did not reveal it");
            Assert.IsNotNull(world.ModuleAt(science));
            string signature = world.Layout.Signature();
            Assert.IsFalse(world.RevealScience());
            Assert.AreEqual(signature, world.Layout.Signature());
            yield return null;
            AssertLaidOut(model);
        }

        [UnityTest]
        public IEnumerator DebugF2_ShowsLabelledMarkersOnEverySlot()
        {
            yield return LoadCubeScene();
            var debug = UnityEngine.Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsNotNull(debug);
            Assert.IsFalse(debug.SlotMarkersVisible, "Slot markers should start hidden");

            yield return this.Tap(keyboard.f2Key, holdFrames: 2);
            Assert.IsTrue(debug.SlotMarkersVisible, "F2 did not turn slot markers on");

            // The run starts in Town's town (4.5), which has only its exits; the ruin screen west of it has every kind of slot.
            ScreenModule start = world.ModuleAt(navigator.Current);
            Assert.IsNotNull(start);
            Assert.IsTrue(start.IsTownScreen, "The start screen is Town's town");
            Assert.IsEmpty(start.Gates, "A town has no gate slot");
            Assert.IsEmpty(start.HiddenItems, "A town has no hidden spot");
            var beside = new ScreenAddress(CubeModel.StartFace, 0, 1);
            foreach (ScreenAddress screen in new[] { navigator.Current, beside })
            {
                ScreenModule here = world.ModuleAt(screen);
                var active = here.AllSlots.Where(s => s.gameObject.activeInHierarchy).ToList();
                int openSides = Enum.GetValues(typeof(Facing)).Cast<Facing>()
                    .Count(f => world.Model.TryStep(screen, f, out _, out _));
                Assert.AreEqual(openSides, active.OfType<ExitSlot>().Count(), $"{screen}: one active exit per open side");
                if (!here.IsTownScreen)
                {
                    Assert.GreaterOrEqual(active.OfType<GateSlot>().Count(), 1);
                    Assert.AreEqual(1, active.OfType<HiddenItemSlot>().Count());
                }
                foreach (ModuleSlot slot in active)
                {
                    Assert.IsTrue(slot.MarkerVisible, $"{slot.Label} on {here.name} has no visible marker");
                    Assert.IsNotEmpty(slot.Label);
                }
            }

            // Modules created later (the science reveal) get markers too, including the active core entrance.
            world.RevealScience();
            yield return null;
            int coreMarkers = 0;
            foreach (ScreenModule module in world.AllModules)
            {
                foreach (ModuleSlot slot in module.AllSlots.Where(s => s.gameObject.activeInHierarchy))
                {
                    Assert.IsTrue(slot.MarkerVisible, $"{slot.Label} on {module.name} has no visible marker");
                    if (slot is CoreEntranceSlot) coreMarkers++;
                }
            }
            Assert.AreEqual(5, coreMarkers, "One core-entrance marker per built science face");

            yield return this.Tap(keyboard.f2Key, holdFrames: 2);
            Assert.IsFalse(debug.SlotMarkersVisible, "F2 did not turn slot markers off");
            Assert.IsTrue(world.AllModules.SelectMany(m => m.AllSlots).All(s => !s.MarkerVisible));
        }

        [UnityTest]
        public IEnumerator WalkTowardSealedFace_IsBlocked()
        {
            yield return LoadCubeScene();
            // Nothing is sealed in play since Earth was built; the sealed-face path is kept for faces sealed on purpose.
            // Seal Earth & Atmosphere and start a run on a seed where it borders Town.
            Theme[] sealedThemes = { Theme.EarthAtmosphere };
            int seed = Enumerable.Range(1, 200).First(s =>
            {
                var m = new CubeModel(s, CubeSettings.DefaultFaceSize, sealedThemes);
                return new[] { Facing.East, Facing.North, Facing.West, Facing.South }.Any(d => m.IsSealed(CubeModel.NeighborFace(CubeModel.StartFace, d)));
            });
            world.SealedThemes.Clear();
            world.SealedThemes.AddRange(sealedThemes);
            try
            {
                world.Rebuild(seed);
            }
            finally
            {
                world.SealedThemes.Clear();
            }
            yield return null;
            yield return null;
            CubeModel model = world.Model;
            Assert.IsTrue(model.IsSealed(model.FaceOf(Theme.EarthAtmosphere)), "Earth is sealed for this run");

            // Earth & Atmosphere is sealed and borders Town on this seed.
            Facing direction = new[] { Facing.East, Facing.North, Facing.West, Facing.South }
                .First(d => model.IsSealed(CubeModel.NeighborFace(CubeModel.StartFace, d)));

            // Walk until the player stops advancing (pressed against the wall), with a generous timeout.
            Press(KeyFor(keyboard, direction), queueEventOnly: true);
            float end = Time.realtimeSinceStartup + 20f;
            Vector3 last = navigator.transform.position;
            float stillSince = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < end && Time.realtimeSinceStartup - stillSince < 0.5f)
            {
                yield return null;
                Vector3 now = navigator.transform.position;
                if ((now - last).sqrMagnitude > 1e-6f) stillSince = Time.realtimeSinceStartup;
                last = now;
            }
            Release(KeyFor(keyboard, direction), queueEventOnly: true);
            yield return null;

            Assert.AreEqual(CubeModel.StartFace, navigator.Face, $"Player crossed into sealed {CubeModel.NeighborFace(CubeModel.StartFace, direction)}");
            Assert.AreEqual(0, navigator.Crossings);
            Assert.AreEqual(ExitScreen(direction), navigator.Current, "Player should be stopped at the sealed edge");

            // Actually against the sealed edge: within wall thickness + collider radius (+ epsilon) of it.
            const float reach = ScreenEdges.EdgeWallThickness + TimeField.PlayerRadius + 0.05f;
            Vector2 extent = world.FaceExtent;
            Vector2 local = (Vector2)navigator.transform.position - world.FaceOrigin(CubeModel.StartFace);
            Assert.That(local.x, Is.InRange(0f, extent.x));
            Assert.That(local.y, Is.InRange(0f, extent.y));
            switch (direction)
            {
                case Facing.East: Assert.GreaterOrEqual(local.x, extent.x - reach, "Player did not reach the east wall"); break;
                case Facing.West: Assert.LessOrEqual(local.x, reach, "Player did not reach the west wall"); break;
                case Facing.North: Assert.GreaterOrEqual(local.y, extent.y - reach, "Player did not reach the north wall"); break;
                default: Assert.LessOrEqual(local.y, reach, "Player did not reach the south wall"); break;
            }
        }

        [UnityTest]
        public IEnumerator DebugKeys_F5Rerolls_F6JumpsToAnotherUnsealedScreen()
        {
            yield return LoadCubeScene();
            var debug = UnityEngine.Object.FindAnyObjectByType<CubeDebug>();
            Assert.IsNotNull(debug, "CubeDebug not found in the Cube scene");
            Assert.IsTrue(debug.enabled, "Debug tools must be active in the editor");

            int oldSeed = world.Seed;
            yield return this.Tap(keyboard.f5Key, holdFrames: 2);
            Assert.AreNotEqual(oldSeed, world.Seed, "F5 did not reroll the seed");
            Assert.AreEqual(world.Seed, world.Model.Seed);
            Assert.AreEqual(world.Model.StartScreen, navigator.Current, "Reroll should return the player to the start screen");
            Assert.IsFalse(world.ScienceRevealed, "A reroll starts a new run with the science faces unrevealed");

            ScreenAddress before = navigator.Current;
            yield return this.Tap(keyboard.f6Key, holdFrames: 2);
            ScreenAddress after = navigator.Current;
            Assert.AreNotEqual(before, after, "F6 did not move the player");
            Assert.IsFalse(world.Model.IsSealed(after.Face), "F6 jumped onto a sealed face");

            bool overlay = debug.OverlayVisible;
            Press(keyboard.f1Key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(keyboard.f1Key, queueEventOnly: true);
            Assert.AreNotEqual(overlay, debug.OverlayVisible, "F1 did not toggle the overlay");
        }

        /// <summary>The look a hint should name for a gate the reveal built, read from its component alone.</summary>
        private static string BuiltLook(Gate gate)
        {
            switch (gate)
            {
                case BreakableGate b: return b.Look == BreakableLook.Pot ? GateLooks.Pot : GateLooks.Rock;
                case FlowerVineGate _: return GateLooks.Bramble;
                case ButtonGate _: return GateLooks.ButtonDoor;
                case DarkRoomGate _: return GateLooks.DarkDoorway;
                case CrackedWallGate _: return GateLooks.CrackedWall;
                case LeadPlateGate _: return GateLooks.PlatedDoor;
                case TimedDoorGate _: return GateLooks.SwingingDoor;
                case GapGate _: return GateLooks.Gap;
                case PhenomenonGate p: return GateLooks.Of(p.Kind);
                default: return gate.GetType() == typeof(Gate) ? GateLooks.Plain : $"(unknown gate {gate.GetType().Name})";
            }
        }

        /// <summary>
        /// One source of truth for gate looks: on seeds 1-50, every required gate's hint look (from the face plans, as
        /// RunFacts gives it to the hints) is the look of the gate the reveal actually built in that slot.
        /// </summary>
        [UnityTest]
        public IEnumerator GateLooks_HintsNameTheGateTheRevealBuilt_OnSeeds1To50()
        {
            yield return LoadCubeScene();
            var looks = new HashSet<string>();
            int count = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                world.Rebuild(seed);
                Assert.IsTrue(world.RevealScience(), $"seed {seed}: the reveal");
                RunFacts facts = RunFacts.Of(world);
                foreach (GatePlacement g in world.ItemPlacement.Gates)
                {
                    ScreenModule module = world.ModuleAt(g.Screen);
                    Assert.IsNotNull(module, $"seed {seed}: {g}");
                    Gate[] built = module.Gates[g.Slot].GetComponentsInChildren<Gate>(true);
                    Assert.AreEqual(1, built.Length, $"seed {seed}: one gate in {g}");
                    string look = BuiltLook(built[0]);
                    Assert.AreEqual(look, HintGenerator.LookOf(facts, g), $"seed {seed}: the hint look of {g}");
                    looks.Add(look);
                    count++;
                }
                yield return null; // let the last run's objects go
            }
            Assert.Greater(count, 250, "Every required gate of 50 runs");
            Assert.GreaterOrEqual(looks.Count, 8, $"Gates of every face's kinds: {string.Join(", ", looks)}");
        }
    }
}
