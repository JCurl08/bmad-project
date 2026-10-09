using System;
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
using Object = UnityEngine.Object;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The temples (4.14) in the real Cube scene: a sealed temple door turns the player away, the leader's hand-in unseals
    /// it, walking in (W) goes down to its room off the net and walking out (S) comes back in front of its door facing away,
    /// with no bounce from a held key; leaving an unsolved temple resets its puzzle, a solved one stays solved. Each puzzle
    /// family is solved with the real keys: an ordering temple (Town's tokens, Biology's seeds, Chemistry's stones against
    /// their timer, a wrong strike putting all back), the ice and the wind (the solver's route, move by move), the island
    /// network (the solver's trail, plank by plank) and Physics' booths (a boulder grabbed with the temple's grip, then each
    /// door opened by its switch and a dash); solving plays the magic moment and gives the gem, which then helps in the core.
    /// </summary>
    public class TempleSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private const float K = ScreenMath.ContentScale;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private TempleRooms temples;
        private CoreArena arena;
        private ScreenCamera screenCamera;
        private Rigidbody2D body;
        private PlayerMover mover;
        private RunRewards rewards;
        private IceSlider slider;
        private WindRider rider;
        private float baseSpeed;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            GamePause.Resume();
            Time.timeScale = 1f;
            DialogueBox box = DialogueBox.Shared;
            if (box != null && box.IsOpen) box.Close();
            base.TearDown();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            temples = Object.FindAnyObjectByType<TempleRooms>();
            arena = Object.FindAnyObjectByType<CoreArena>();
            screenCamera = Object.FindAnyObjectByType<ScreenCamera>();
            body = navigator.GetComponent<Rigidbody2D>();
            mover = navigator.GetComponent<PlayerMover>();
            rewards = navigator.GetComponent<RunRewards>();
            slider = navigator.GetComponent<IceSlider>();
            rider = navigator.GetComponent<WindRider>();
            baseSpeed = mover.Speed;
            Assert.IsNotNull(temples, "The Cube scene has TempleRooms");
            Assert.AreSame(world, temples.World);
            Assert.AreSame(navigator, temples.Navigator);
            NoFaceEnemies();
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private void NoFaceEnemies()
        {
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
            world.GetComponent<MathFace>().SpawnEnemies = false;
            world.GetComponent<EarthFace>().SpawnEnemies = false;
        }

        private IEnumerator Rebuild(int seed, bool reveal)
        {
            world.Rebuild(seed);
            yield return null;
            NoFaceEnemies();
            if (reveal) world.RevealScience();
            yield return null;
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(2);
        }

        private ScreenAddress TownOf(FaceId face) => new ScreenAddress(face, TownSquare.Cell(world.FaceSize));

        private TempleDoor DoorOf(FaceId face) => world.TownViewOf(face).TempleDoor;

        private IEnumerator PutPlayerAt(Vector2 at)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Unseals a face's temple and walks into it with W from in front of its door (the real keys).</summary>
        private IEnumerator WalkIn(FaceId face)
        {
            ScreenAddress town = TownOf(face);
            navigator.TeleportTo(town, Facing.North);
            yield return PhysicsSteps(2);
            // 4.14 review: unsealed through the real hand-in (every fragment held, then a talk with the face's leader), so the
            // temple's quest joins the log as in play.
            for (int i = 1; i <= world.Dice.Of(face); i++) rewards.Grant(Reward.Fragment(i), face);
            NpcTalker leader = face == CubeModel.StartFace
                ? Object.FindAnyObjectByType<TownPopulation>().Leader
                : world.GetComponent<TownSquares>().LeaderOf(face);
            leader.GetComponent<LeaderQuest>().Talk(out QuestStep step);
            Assert.AreEqual(QuestStep.Unsealed, step, $"{face}: the hand-in unseals the temple");
            Assert.IsTrue(world.Repairs.IsUnsealed(face));
            QuestEntry quest = world.Quests.Get(QuestLog.TempleId(face));
            Assert.IsNotNull(quest, "the temple's quest joins the log");
            Assert.IsFalse(quest.Done);
            TempleDoor door = DoorOf(face);
            yield return PutPlayerAt(TempleRooms.ReturnPoint(door));
            yield return PhysicsSteps(2);
            yield return this.HoldUntil(keyboard.wKey, () => navigator.InTemple, 4f);
            Assert.IsTrue(navigator.InTemple, $"{face}: walked into its temple");
            yield return PhysicsSteps(2);
        }

        /// <summary>Walks toward a point with the 8-way keys until within reach (the real keys), then lets go.</summary>
        private IEnumerator WalkToward(Vector2 target, float within, float timeout = 8f)
        {
            float end = Time.time + timeout;
            Key[] held = null;
            while (Time.time < end)
            {
                Vector2 d = target - body.position;
                if (d.magnitude <= within) break;
                var dir = new Vector2Int(Mathf.Abs(d.x) > 0.12f ? Math.Sign(d.x) : 0, Mathf.Abs(d.y) > 0.12f ? Math.Sign(d.y) : 0);
                Key[] keys = WasdKeys(dir);
                if (held == null || !keys.SequenceEqual(held))
                {
                    SetKeys(keyboard, keys);
                    held = keys;
                }
                yield return null;
            }
            SetKeys(keyboard);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>
        /// Strikes a target with the real keys: walks up to it along the nearest of the eight ways (so the player ends facing
        /// it), then swings (Enter).
        /// </summary>
        private IEnumerator StrikeTarget(Vector2 target)
        {
            Vector2 d = target - body.position;
            float a = Mathf.Round(Mathf.Atan2(d.y, d.x) / (Mathf.PI / 4f)) * (Mathf.PI / 4f);
            var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            yield return WalkToward(target - way * 1.6f, 0.25f);
            // The last step straight at it, so the facing points at the target.
            var dir = new Vector2Int(Mathf.RoundToInt(way.x), Mathf.RoundToInt(way.y));
            SetKeys(keyboard, WasdKeys(dir));
            yield return WaitUntilOrGameTimeout(() => Vector2.Distance(body.position, target) < 1.15f, 1f);
            SetKeys(keyboard);
            yield return null;
            yield return new WaitForFixedUpdate();
            // The swing (once the last one's cooldown is over): Enter down for two frames, the keyboard's whole state in one
            // event (as SetKeys moves).
            PlayerAttack attack = navigator.GetComponent<PlayerAttack>();
            yield return WaitUntilOrGameTimeout(() => Time.time - lastSwing >= attack.Cooldown + 0.02f, 1f);
            int swings = attack.SwingCount;
            SetKeys(keyboard, Key.Enter);
            yield return null;
            yield return null;
            SetKeys(keyboard);
            yield return null;
            yield return null;
            Assert.AreEqual(swings + 1, attack.SwingCount, "the swing happened");
            lastSwing = Time.time;
        }

        private float lastSwing = float.NegativeInfinity;

        // ---------------------------------------------------------------- the door, the way in and out, the reset

        [UnityTest]
        public IEnumerator ASealedTemple_TurnsThePlayerAway_TheHandInUnsealsIt_InAndOut_WithAHeldKey_AndAnUnsolvedOneResets()
        {
            yield return Load();
            yield return Rebuild(7, false);
            FaceId town = CubeModel.StartFace;
            TownSquareView view = world.TownViewOf(town);
            TempleDoor door = view.TempleDoor;
            Assert.IsNotNull(door, "Town has a temple door");
            Assert.IsFalse(door.Unsealed);
            Assert.AreEqual(TempleLook.Sealed, view.TempleLook);
            Assert.AreSame(TempleArt.Building(Theme.Town, TempleLook.Sealed), view.TempleArtRenderer.sprite, "drawn sealed");

            // Walking into the sealed door: turned away, with the sealed popup.
            navigator.TeleportTo(TownOf(town), Facing.North);
            yield return PhysicsSteps(2);
            yield return PutPlayerAt(TempleRooms.ReturnPoint(door));
            int bumps = temples.SealedBumps;
            yield return this.HoldUntil(keyboard.wKey, () => temples.SealedBumps > bumps, 3f);
            yield return PhysicsSteps(10);
            Assert.IsFalse(navigator.InTemple, "a sealed temple never lets the player in");
            Assert.Greater(temples.SealedBumps, bumps);
            Assert.IsTrue(PickupPopup.Active.Any(p => p.Text == TempleText.SealedPopup), "it says so");

            // The hand-in (the Mayor, with Town's fragment): the temple opens, and the quest log gains it.
            Assert.IsTrue(rewards.Grant(Reward.Fragment(1), town));
            LeaderQuest mayor = Object.FindAnyObjectByType<TownPopulation>().Leader.GetComponent<LeaderQuest>();
            mayor.Talk(out QuestStep step);
            Assert.AreEqual(QuestStep.Unsealed, step);
            Assert.IsTrue(door.Unsealed, "the hand-in unseals the temple");
            Assert.AreEqual(TempleLook.Open, view.TempleLook);
            Assert.IsTrue(world.Quests.Has(QuestLog.TempleId(town)));
            Assert.IsFalse(world.Repairs.IsRepaired(town));

            // In with W (held): the camera on the temple's own screen, the player's screen still the town's; W still held
            // does not walk on (a fresh press is awaited).
            yield return PutPlayerAt(TempleRooms.ReturnPoint(door));
            Press(keyboard.wKey, queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => navigator.InTemple, 4f);
            Assert.IsTrue(navigator.InTemple);
            Vector2 arrival = body.position;
            yield return WaitRealSeconds(0.5f);
            Release(keyboard.wKey, queueEventOnly: true);
            yield return null;
            Assert.Less(Vector2.Distance(arrival, body.position), 0.05f, "a held W waits for a fresh press inside");
            TempleView room = temples.Current;
            Assert.AreEqual(TempleRoom.RoomCentre(town), room.Centre);
            Assert.AreEqual(TownOf(town), navigator.Current, "the player's screen stays the town's");
            Assert.AreEqual(TempleRoom.RoomScreen(town), ScreenMath.ScreenIndex(screenCamera.transform.position), "the camera frames the temple");
            Assert.IsTrue(PickupPopup.Active.Any(p => p.Text == TempleText.Rule(TemplePuzzleKind.TownOrder)), "the rule is said");

            // One right strike, then out with S held: back in front of the door, facing away, staying put while S is held.
            Assert.IsTrue(room.Targets.Strike(room.Targets.Targets[0]));
            Assert.AreEqual(1, room.Targets.Progress);
            Press(keyboard.sKey, queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => !navigator.InTemple, 6f);
            Assert.IsFalse(navigator.InTemple, "walked out of the doorway");
            Vector2 back = body.position;
            Assert.Less(Vector2.Distance(back, TempleRooms.ReturnPoint(door)), 0.05f, "back in front of the temple's door");
            Assert.AreEqual(Facing.South, navigator.Facing, "facing away from it");
            yield return WaitRealSeconds(0.5f);
            Release(keyboard.sKey, queueEventOnly: true);
            yield return PhysicsSteps(3);
            Assert.Less(Vector2.Distance(back, body.position), 0.05f, "a held S never walks on, nor back in");
            Assert.IsFalse(navigator.InTemple);
            Assert.IsFalse(temples.Rooms.ContainsKey(town), "an unsolved temple is thrown away on leaving");

            // Back in: the puzzle starts afresh.
            yield return this.HoldUntil(keyboard.wKey, () => navigator.InTemple, 4f);
            Assert.IsTrue(navigator.InTemple);
            Assert.AreEqual(0, temples.Current.Targets.Progress, "the puzzle reset");
            Assert.IsTrue(temples.Current.Targets.Targets.All(t => !t.Done));

            // A teleport out (F6) resets it too; solve it, and it stays solved.
            temples.Current.Targets.Strike(temples.Current.Targets.Targets[0]);
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(3);
            Assert.IsNull(temples.Current);
            Assert.IsFalse(temples.Rooms.ContainsKey(town));
            Assert.IsTrue(temples.Enter(door));
            TempleView fresh = temples.Current;
            Assert.AreEqual(0, fresh.Targets.Progress);
            foreach (TempleTarget t in fresh.Targets.Targets) fresh.Targets.Strike(t);
            Assert.IsTrue(fresh.IsSolved);
            Assert.IsTrue(world.Repairs.HasGem(town));
            Assert.IsTrue(temples.Leave());
            yield return PhysicsSteps(2);
            Assert.IsTrue(temples.Enter(door));
            Assert.AreSame(fresh, temples.Current, "a solved temple stays as it is");
            Assert.IsTrue(temples.Current.IsSolved);
            Assert.IsTrue(temples.Leave());

            // A new run forgets it all.
            world.Rebuild(8);
            yield return PhysicsSteps(2);
            Assert.AreEqual(0, temples.Rooms.Count);
            Assert.IsFalse(world.TownViewOf(town).TempleDoor.Unsealed);
        }

        // ---------------------------------------------------------------- the ordering temples, with the real keys

        private IEnumerator SolveOrderWithKeys(FaceId face, bool wrongFirst)
        {
            yield return WalkIn(face);
            TempleView room = temples.Current;
            TempleTargets targets = room.Targets;
            Assert.IsNotNull(targets, $"{room.Puzzle.Kind}: a row of targets");
            if (wrongFirst)
            {
                // A wrong strike (the second target first): every target goes back, no damage.
                float hearts = navigator.GetComponent<Health>().Current;
                yield return StrikeTarget(room.Centre + targets.Spec.Spots[1]);
                Assert.AreEqual(1, targets.Resets, "a wrong strike puts every target back");
                Assert.AreEqual(0, targets.Progress);
                Assert.AreEqual(hearts, navigator.GetComponent<Health>().Current, "no damage");
                Assert.IsTrue(PickupPopup.Active.Any(p => p.Text == TempleText.WrongPopup));
                // 4.14 review: a second wrong strike replaces the popup rather than stacking one more; the rule is not said again.
                yield return StrikeTarget(room.Centre + targets.Spec.Spots[2]);
                Assert.AreEqual(2, targets.Resets);
                yield return null;
                Assert.AreEqual(1, PickupPopup.Active.Count(p => p != null && p.Text == TempleText.WrongPopup), "one wrong-strike popup at a time");
                Assert.LessOrEqual(PickupPopup.Active.Count(p => p != null && p.Text == TempleText.Rule(room.Puzzle.Kind)), 1, "the rule, once per attempt");
            }
            var magicSeen = false;
            room.Solved += _ => magicSeen = true;
            for (int i = 0; i < targets.Targets.Count; i++)
            {
                yield return StrikeTarget(room.Centre + targets.Spec.Spots[i]);
                Assert.AreEqual(i + 1, targets.Progress, $"{room.Puzzle.Kind}: target {i + 1} struck in its turn");
                Assert.IsTrue(targets.Targets[i].Done);
            }
            Assert.IsTrue(targets.IsSolved);
            Assert.IsTrue(magicSeen && room.IsSolved, "solved");
        }

        [UnityTest]
        public IEnumerator TheOrderingTemples_AreSolvedWithTheRealKeys_AndAWrongStrikePutsAllBack([Values(Theme.Town, Theme.Biology, Theme.Chemistry)] Theme theme)
        {
            yield return Load();
            yield return Rebuild(21, theme != Theme.Town);
            FaceId face = world.Model.FaceOf(theme);
            yield return SolveOrderWithKeys(face, true);
            TempleView room = temples.Current;
            if (theme == Theme.Chemistry) Assert.Greater(room.Targets.Spec.TimerSeconds, 0f, "the stones run against a timer");
            yield return MagicAndGem(room, face, theme);
        }

        /// <summary>The magic moment: the fragments spiral in and mend, the gem flies to the player; the face is mended.</summary>
        private IEnumerator MagicAndGem(TempleView room, FaceId face, Theme theme)
        {
            Assert.IsNotNull(room.Magic, "the magic moment plays");
            Assert.AreEqual(world.Dice.Of(face), room.Shards.Count, "one fragment per die pip");
            Assert.IsTrue(world.Repairs.IsRepaired(face), "the face is mended");
            Assert.IsTrue(world.Repairs.HasGem(face), "and its gem is the player's");
            Assert.IsTrue(world.TownViewOf(face).Repaired, "the statue is gold");
            Assert.IsTrue(world.Quests.Has(QuestLog.TempleId(face)), "the temple's quest is in the log");
            Assert.IsTrue(world.Quests.IsDone(QuestLog.TempleId(face)), "and the solve marks it done");
            Assert.AreEqual(QuestText.TempleDone(theme), world.Quests.Get(QuestLog.TempleId(face)).Step);
            Assert.AreEqual(BossBenefits.Of(theme), arena.RunBenefits & BossBenefits.Of(theme), "its benefit waits in the core");
            yield return WaitUntilOrGameTimeout(() => room.Magic.Done, 4f);
            Assert.IsTrue(room.Magic.Done, "the moment ends");
            Assert.IsTrue(room.Shards.All(s => !s.enabled), "the fragments mended into the gem");
            Assert.IsFalse(room.Magic.Gem.enabled, "and the gem flew to the player");
        }

        [UnityTest]
        public IEnumerator TheStoneTemple_GoesDarkWhenItsTimerRunsOut()
        {
            yield return Load();
            yield return Rebuild(21, true);
            FaceId face = world.Model.FaceOf(Theme.Chemistry);
            yield return WalkIn(face);
            TempleTargets targets = temples.Current.Targets;
            Assert.IsTrue(targets.Strike(targets.Targets[0]));
            Assert.IsTrue(targets.TimerRunning);
            yield return WaitUntilOrGameTimeout(() => !targets.TimerRunning, targets.Spec.TimerSeconds + 2f);
            Assert.AreEqual(0, targets.Progress, "too slow: they all go dark");
            Assert.AreEqual(1, targets.Resets);
            Assert.IsTrue(PickupPopup.Active.Any(p => p.Text.StartsWith(TempleText.TooSlowPopup)));
        }

        // ---------------------------------------------------------------- ice and wind

        private static int SeedWithEarthKind(TemplePuzzleKind kind)
        {
            for (int seed = 1; seed < 200; seed++)
            {
                var model = new CubeModel(seed);
                if (TemplePlan.For(model, model.FaceOf(Theme.EarthAtmosphere)).Kind == kind) return seed;
            }
            return -1;
        }

        [UnityTest]
        public IEnumerator AnIceTemple_IsSolvedWithTheRealKeys_MoveByMove()
        {
            yield return Load();
            int seed = SeedWithEarthKind(TemplePuzzleKind.Ice);
            Assert.Greater(seed, 0);
            yield return Rebuild(seed, true);
            FaceId face = world.Model.FaceOf(Theme.EarthAtmosphere);
            yield return WalkIn(face);
            TempleView room = temples.Current;
            IceLayout ice = room.Puzzle.Ice;
            Assert.IsNotNull(room.Ice);
            yield return this.HoldUntil(keyboard.wKey, () => slider.Screen != null, 4f);
            yield return WaitUntilOrTimeout(() => slider.AtRest, 4f);
            Assert.AreSame(room.Ice, slider.Screen, "the temple's ice drives the player (IceSlider works in a temple)");
            Assert.AreEqual(ice.Mouth(Facing.South), ice.NodeOf(slider.Cell), "on the south lane");
            List<IceStep> route = IceRoute(ice, ice.Mouth(Facing.South));
            Assert.IsNotNull(route);
            Assert.AreEqual(ice.Measure(Facing.South).MinSlides, route.Count, "the solver's fewest slides");
            foreach (IceStep s in route)
            {
                for (int guard = 0; guard < 12 && slider.Cell != s.Cell; guard++)
                {
                    Vector2Int d = IceLayout.Pos(s.Cell) - IceLayout.Pos(slider.Cell);
                    yield return IceMove(d.x > 0 ? Facing.East : d.x < 0 ? Facing.West : d.y > 0 ? Facing.North : Facing.South);
                }
                Assert.AreEqual(s.Cell, slider.Cell);
                yield return IceMove(s.Facing);
                Assert.AreEqual(s.End, slider.Cell, $"from {IceLayout.Pos(s.Cell)} toward {s.Facing}");
            }
            Assert.IsTrue(room.Ice.GoalReached);
            Assert.IsTrue(room.IsSolved, "resting on the pad solves the temple");
            yield return MagicAndGem(room, face, Theme.EarthAtmosphere);
        }

        private struct IceStep
        {
            public int From, Cell, End;
            public Facing Facing;
        }

        private static List<IceStep> IceRoute(IceLayout ice, int start)
        {
            var prev = new Dictionary<int, IceStep> { [start] = default };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                foreach (int c in ice.CellsOf(u))
                    for (int f = 0; f < 4; f++)
                    {
                        int to = ice.Slide(c, (Facing)f, out int end, out _);
                        if (to < 0 || prev.ContainsKey(to)) continue;
                        prev[to] = new IceStep { From = u, Cell = c, End = end, Facing = (Facing)f };
                        if (to == ice.Goal)
                        {
                            var route = new List<IceStep>();
                            for (int at = to; at != start; at = prev[at].From) route.Insert(0, prev[at]);
                            return route;
                        }
                        queue.Enqueue(to);
                    }
            }
            return null;
        }

        private IEnumerator IceMove(Facing facing)
        {
            yield return this.TapThroughPhysics(KeyFor(keyboard, facing));
            yield return WaitUntilOrTimeout(() => slider.AtRest, 6f);
        }

        [UnityTest]
        public IEnumerator AWindTemple_IsSolvedWithTheRealKeys_RideByRide()
        {
            yield return Load();
            int seed = SeedWithEarthKind(TemplePuzzleKind.Wind);
            Assert.Greater(seed, 0);
            yield return Rebuild(seed, true);
            FaceId face = world.Model.FaceOf(Theme.EarthAtmosphere);
            yield return WalkIn(face);
            TempleView room = temples.Current;
            WindLayout wind = room.Puzzle.Wind;
            Assert.IsNotNull(room.Wind);
            yield return this.HoldUntil(keyboard.wKey, () => rider.Screen != null, 4f);
            yield return WaitUntilOrTimeout(() => rider.AtRest, 4f);
            Assert.AreSame(room.Wind, rider.Screen, "the temple's wind drives the player (WindRider works in a temple)");
            List<WindRide> route = WindRoute(wind, wind.NodeOf(rider.Cell));
            Assert.IsNotNull(route);
            foreach (WindRide r in route)
            {
                foreach (Facing f in WindWalk(wind, rider.Cell, r.Cell)) yield return WindMove(f);
                Assert.AreEqual(r.Cell, rider.Cell);
                yield return WindMove(r.Facing);
                Assert.AreEqual(r.End, rider.Cell, $"from {WindLayout.Pos(r.Cell)} toward {r.Facing}");
            }
            if (rider.Cell != wind.Goal)
                foreach (Facing f in WindWalk(wind, rider.Cell, wind.Goal)) yield return WindMove(f);
            yield return PhysicsSteps(3);
            Assert.IsTrue(room.Wind.GoalReached);
            Assert.IsTrue(room.IsSolved, "reaching the ring solves the temple");
            yield return MagicAndGem(room, face, Theme.EarthAtmosphere);
        }

        private struct WindRide
        {
            public int From, Cell, End;
            public Facing Facing;
        }

        private static List<WindRide> WindRoute(WindLayout wind, int start)
        {
            int goal = wind.NodeOf(wind.Goal);
            if (start == goal) return new List<WindRide>();
            var prev = new Dictionary<int, WindRide> { [start] = default };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                foreach (int c in wind.CellsOf(u))
                    for (int f = 0; f < 4; f++)
                    {
                        Vector2Int p = WindLayout.Pos(c) + WindLayout.Step((Facing)f);
                        if (!WindLayout.InGrid(p)) continue;
                        int end = wind.RideFrom(c, WindLayout.Index(p), out _);
                        if (end < 0) continue;
                        int to = wind.NodeOf(end);
                        if (prev.ContainsKey(to)) continue;
                        prev[to] = new WindRide { From = u, Cell = c, End = end, Facing = (Facing)f };
                        if (to == goal)
                        {
                            var route = new List<WindRide>();
                            for (int at = to; at != start; at = prev[at].From) route.Insert(0, prev[at]);
                            return route;
                        }
                        queue.Enqueue(to);
                    }
            }
            return null;
        }

        private static List<Facing> WindWalk(WindLayout wind, int from, int to)
        {
            var prev = new Dictionary<int, (int Cell, Facing Way)> { [from] = (-1, Facing.North) };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                if (u == to) break;
                for (int f = 0; f < 4; f++)
                {
                    Vector2Int p = WindLayout.Pos(u) + WindLayout.Step((Facing)f);
                    if (!WindLayout.InGrid(p)) continue;
                    int n = WindLayout.Index(p);
                    if (prev.ContainsKey(n) || !wind.IsWalkable(n) || wind.NodeOf(n) != wind.NodeOf(from)) continue;
                    if (n == wind.Goal && n != to) continue;
                    prev[n] = (u, (Facing)f);
                    queue.Enqueue(n);
                }
            }
            Assert.IsTrue(prev.ContainsKey(to), $"a walk from {WindLayout.Pos(from)} to {WindLayout.Pos(to)}");
            var steps = new List<Facing>();
            for (int at = to; at != from; at = prev[at].Cell) steps.Insert(0, prev[at].Way);
            return steps;
        }

        private IEnumerator WindMove(Facing facing)
        {
            yield return this.TapThroughPhysics(KeyFor(keyboard, facing));
            yield return WaitUntilOrTimeout(() => rider.AtRest, 6f);
        }

        // ---------------------------------------------------------------- islands

        [UnityTest]
        public IEnumerator AnIslandTemple_IsLitWithTheRealKeys_PlankByPlank()
        {
            yield return Load();
            yield return Rebuild(7, true);
            FaceId face = world.Model.FaceOf(Theme.Math);
            yield return WalkIn(face);
            TempleView room = temples.Current;
            IslandScreen screen = room.Islands;
            IslandLayout layout = room.Puzzle.Islands;
            Assert.IsNotNull(screen);
            int door = layout.DoorIsland(Facing.South);
            Assert.AreEqual(door, layout.IslandAt(body.position - room.Centre), "arrived on the south doorway's island");

            // To a clean start, then the solver's trail with the keys, one plank (one straight walk) at a time.
            int start = layout.Solver.Starts.Where(layout.Solver.CleanStart).OrderBy(s => layout.Solver.Hops(door, s, false, true)).First();
            List<int> toStart = IslandRoute(layout, door, start);
            for (int i = 1; i < toStart.Count; i++) yield return IslandWalk(room, layout, toStart[i]);
            if (screen.Lights.LitCount > 0)
            {
                // Out over a dark plank and back over it: the second crossing snuffs everything, on the start.
                int k = layout.NetworkPlanks.First(p => layout.Bridges[p].Joins(start) && screen.Lights[p] == PlankLight.Unlit);
                yield return IslandWalk(room, layout, layout.Bridges[k].Other(start));
                yield return IslandWalk(room, layout, start);
                yield return new WaitForSeconds(IslandScreen.FlashSeconds + 0.1f);
            }
            Assert.AreEqual(0, screen.Lights.LitCount, "a clean start");
            List<int> trail = layout.Solver.Trail(start);
            int at = start;
            for (int i = 0; i < trail.Count; i++)
            {
                int next = layout.Bridges[trail[i]].Other(at);
                yield return IslandWalk(room, layout, next);
                Assert.AreEqual(i + 1, screen.Lights.LitCount, $"plank {i + 1} of {trail.Count} lit");
                at = next;
            }
            Assert.IsTrue(screen.Lights.Solved);
            yield return WaitUntilOrGameTimeout(() => room.IsSolved, IslandScreen.ChainSeconds + 1f);
            Assert.IsTrue(room.IsSolved, "the light chain reaches the gem's spot and solves the temple");
            yield return MagicAndGem(room, face, Theme.Math);
        }

        private static List<int> IslandRoute(IslandLayout layout, int from, int to)
        {
            var previous = new Dictionary<int, int> { [from] = from };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int at = queue.Dequeue();
                if (at == to) break;
                foreach (int k in layout.NetworkPlanks)
                {
                    IslandBridge b = layout.Bridges[k];
                    if (!b.Joins(at)) continue;
                    int other = b.Other(at);
                    if (previous.ContainsKey(other)) continue;
                    previous[other] = at;
                    queue.Enqueue(other);
                }
            }
            var route = new List<int> { to };
            while (route[0] != from) route.Insert(0, previous[route[0]]);
            return route;
        }

        /// <summary>One straight walk (a plank lies in one row or column) to an island's spot, slowing at the end (the real keys).</summary>
        private IEnumerator IslandWalk(TempleView room, IslandLayout layout, int island)
        {
            Vector2 target = room.Centre + IslandLayout.SpotCentre(layout.Spots[island]);
            for (int leg = 0; leg < 2; leg++)
            {
                Vector2 d = target - body.position;
                Facing toward = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? (d.x > 0f ? Facing.East : Facing.West) : (d.y > 0f ? Facing.North : Facing.South);
                Vector2 axis = toward.ToVector();
                if (Mathf.Abs(Vector2.Dot(d, axis)) < 0.05f * K) continue;
                float step = Time.fixedDeltaTime;
                yield return null;
                mover.Speed = baseSpeed;
                Press(KeyFor(keyboard, toward), queueEventOnly: true);
                float end = Time.time + d.magnitude / baseSpeed + 1.5f;
                while (Time.time < end)
                {
                    yield return null;
                    float remaining = Vector2.Dot(target - body.position, axis);
                    if (remaining < 0.02f * K) break;
                    mover.Speed = Mathf.Clamp(remaining / step, 0.2f * K, baseSpeed);
                }
                mover.Speed = 0f;
                Release(KeyFor(keyboard, toward), queueEventOnly: true);
                yield return null;
                yield return new WaitForFixedUpdate();
                yield return null;
                mover.Speed = baseSpeed;
            }
            Assert.AreEqual(island, layout.IslandAt(body.position - room.Centre), $"walked onto island {island}");
        }

        // ---------------------------------------------------------------- Physics' booths

        /// <summary>The direction of a door's dash: from its switch toward its latch band.</summary>
        private static Facing DashWay(DoorLayout layout) =>
            TempleBooths.LatchBand(layout).center.x < layout.SwitchLocal.x ? Facing.West : Facing.East;

        /// <summary>Stands the player on a door's switch (the real keys: along the floor below the dash line, then up to it).</summary>
        private IEnumerator ToSwitch(TempleView room, DoorLayout layout, float above = 0f)
        {
            Vector2 sw = room.Centre + layout.SwitchLocal;
            yield return WalkToward(new Vector2(body.position.x, room.Centre.y + Lane), 0.3f);
            yield return WalkToward(new Vector2(sw.x, room.Centre.y + Lane), 0.3f);
            yield return WalkToward(sw + new Vector2(0f, above), 0.15f);
        }

        /// <summary>The dash, with the real keys: held toward the latch band until the door latches (or the time is long up).</summary>
        private IEnumerator Dash(DoorLayout layout, TimedDoorGate door, float seconds = 4f)
        {
            yield return this.HoldUntil(KeyFor(keyboard, DashWay(layout)), () => door.IsOpen, seconds);
            yield return PhysicsSteps(2);
        }

        /// <summary>The players' lane along the bottom of the room (local y), under every boulder and corridor.</summary>
        private const float Lane = -6.4f;

        [UnityTest]
        public IEnumerator ThePhysicsTemple_LendsItsGrip_AndEachBoothOpensWithThreeBoulders_AndADash()
        {
            yield return Load();
            yield return Rebuild(7, true);
            FaceId face = world.Model.FaceOf(Theme.Physics);
            MassMitt mitt = navigator.GetComponent<MassMitt>();
            Assert.IsFalse(mitt.Inventory.Has(world.ItemFor(Theme.Physics)), "no Heavy Mitt* carried");
            yield return WalkIn(face);
            TempleView room = temples.Current;
            PhysicsTrial booths = room.Booths;
            Assert.IsNotNull(booths);
            Assert.IsTrue(mitt.TempleGrip && mitt.IsEquipped, "the temple lends its grip");
            Assert.AreEqual(MassMitt.TempleGrabText, mitt.HudText, "the HUD says how to grab, not the mitt's name");
            Assert.AreEqual(2, room.LatchBands.Count, "a latch band per door");
            Assert.IsNotEmpty(room.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Dash ")), "the dash is drawn on the floor");

            // One boulder grabbed with [Interact] and dragged a little, with the real keys.
            Boulder first = booths.Boulders[0];
            Vector2 was = first.Position;
            yield return WalkToward(first.Position + Vector2.down * 1.4f, 0.3f);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.AreSame(first, mitt.Held, "grabbed without the mitt");
            Assert.AreEqual(MassMitt.TempleDropText, mitt.HudText);
            yield return WalkToward(body.position + Vector2.left * 1.5f, 0.3f);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsNull(mitt.Held, "let go");
            Assert.Greater(Vector2.Distance(was, first.Position), 0.8f, "and it moved");

            for (int i = 0; i < booths.Doors.Count; i++)
            {
                TimedDoorGate door = booths.Doors[i];
                DoorLayout layout = room.Puzzle.Booths.Booths[i].Layout;
                Assert.AreEqual(TempleBooths.BaseSeconds(layout), door.BaseSeconds, 1e-4f, "the door's time is the dash's");
                List<Vector2> spots = TempleBooths.FieldSpots(room.Puzzle.Booths, layout);
                for (int b = 0; b < 3; b++) booths.Boulders[b].Teleport(room.Centre + spots[b * spots.Count / 3]);
                yield return PhysicsSteps(2);
                Assert.AreEqual(3f, door.MassInField, 0.01f, "three boulders in the field");
                yield return ToSwitch(room, layout);
                yield return Dash(layout, door);
                Assert.IsTrue(door.IsOpen, $"booth {i}: reached while open, latched");
            }
            Assert.IsTrue(booths.IsComplete);
            Assert.IsTrue(room.IsSolved);
            yield return MagicAndGem(room, face, Theme.Physics);
            Assert.IsTrue(temples.Leave());
            Assert.IsFalse(mitt.TempleGrip, "the grip stays in the temple");
            Assert.IsNull(mitt.HudText, "and so does its HUD line");
        }

        [UnityTest]
        public IEnumerator APhysicsDash_FromTheSwitchsOuterEdge_StillMakesIt_WithThreeBoulders()
        {
            // 4.14 review: touching the switch well off the line (its outer edge, 0.8 above) and dashing straight on still
            // meets the latch band in time, never a booth.
            yield return Load();
            yield return Rebuild(7, true);
            FaceId face = world.Model.FaceOf(Theme.Physics);
            yield return WalkIn(face);
            TempleView room = temples.Current;
            PhysicsTrial booths = room.Booths;
            for (int i = 0; i < booths.Doors.Count; i++)
            {
                TimedDoorGate door = booths.Doors[i];
                DoorLayout layout = room.Puzzle.Booths.Booths[i].Layout;
                List<Vector2> spots = TempleBooths.FieldSpots(room.Puzzle.Booths, layout);
                for (int b = 0; b < 3; b++) booths.Boulders[b].Teleport(room.Centre + spots[b * spots.Count / 3]);
                yield return PhysicsSteps(2);
                float above = 0.55f * K; // 0.5K or more off the line, still on the switch (SwitchTouch is 0.8K)
                Assert.Less(above, TempleBooths.SwitchTouch);
                Vector2 sw = room.Centre + layout.SwitchLocal;
                yield return WalkToward(new Vector2(body.position.x, room.Centre.y + Lane), 0.3f);
                yield return WalkToward(new Vector2(sw.x, room.Centre.y + Lane), 0.3f);
                int presses = door.Switch.Presses;
                yield return PutPlayerAt(sw + new Vector2(0f, above));
                yield return PhysicsSteps(2);
                Assert.Greater(door.Switch.Presses, presses, "touching the switch at its outer edge presses it");
                Assert.IsTrue(door.IsAjar, "and opens the door");
                yield return Dash(layout, door);
                Assert.IsTrue(door.IsOpen, $"booth {i}: a dash {above:0.00} off the line still latches it");
                Assert.Greater(body.position.y - room.Centre.y, TempleBooths.DashY + 0.4f, "and it was run off the line");
            }
            Assert.IsTrue(room.IsSolved);
        }

        [UnityTest]
        public IEnumerator APhysicsBooth_StaysShutForADash_WithOnlyTwoBoulders()
        {
            yield return Load();
            yield return Rebuild(7, true);
            FaceId face = world.Model.FaceOf(Theme.Physics);
            yield return WalkIn(face);
            TempleView room = temples.Current;
            PhysicsTrial booths = room.Booths;
            TimedDoorGate door = booths.Doors[0];
            DoorLayout layout = room.Puzzle.Booths.Booths[0].Layout;
            List<Vector2> spots = TempleBooths.FieldSpots(room.Puzzle.Booths, layout);
            booths.Boulders[0].Teleport(room.Centre + spots[0]);
            booths.Boulders[1].Teleport(room.Centre + spots[spots.Count - 1]);
            yield return PhysicsSteps(2);
            Assert.AreEqual(2f, door.MassInField, 0.01f);
            yield return ToSwitch(room, layout);
            yield return Dash(layout, door, 3f);
            Assert.IsFalse(door.IsOpen, "one boulder short: shut before the dash ends, even from the switch's near edge on the line");
            Assert.GreaterOrEqual(door.Closings, 1);
        }

        /// <summary>
        /// Drags a boulder with the real keys (4.14 review): along the bottom lane to below it, up, [Interact] to grab, down
        /// to the lane, across under the target, up until the boulder stands on it, and [Interact] to let go.
        /// </summary>
        private IEnumerator Drag(TempleView room, MassMitt mitt, Boulder boulder, Vector2 targetLocal)
        {
            Vector2 c = room.Centre;
            Vector2 under = boulder.Position + Vector2.down * 1.3f;
            yield return WalkToward(new Vector2(body.position.x, c.y + Lane), 0.3f);
            yield return WalkToward(new Vector2(under.x, c.y + Lane), 0.25f);
            yield return WalkToward(under, 0.2f);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.AreSame(boulder, mitt.Held, "grabbed with [Interact]");
            Vector2 offset = boulder.Position - body.position;
            Vector2 goal = c + targetLocal - offset;
            yield return WalkToward(new Vector2(body.position.x, c.y + Lane), 0.25f);
            yield return WalkToward(new Vector2(goal.x, c.y + Lane), 0.2f);
            yield return WalkToward(goal, 0.15f);
            Assert.AreSame(boulder, mitt.Held, "carried all the way");
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsNull(mitt.Held, "let go with [Interact]");
            Assert.Less(Vector2.Distance(boulder.Position, c + targetLocal), 0.6f, "the boulder stands where it was taken");
        }

        /// <summary>Three spots in a field a boulder can be carried up to from the lane: columns apart, none above a booth.</summary>
        private static List<Vector2> CarrySpots(PhysicsTrialSpec spec, DoorLayout layout)
        {
            var chosen = new List<Vector2>();
            foreach (Vector2 p in TempleBooths.FieldSpots(spec, layout).OrderByDescending(p => p.y))
            {
                bool overBooth = spec.Booths.Any(b => p.x > b.Footprint.xMin - 0.7f && p.x < b.Footprint.xMax + 0.7f && p.y > b.Footprint.yMin - 1f);
                if (overBooth || p.y < Lane + 3f || chosen.Any(q => Mathf.Abs(q.x - p.x) < 1.35f)) continue;
                chosen.Add(p);
                if (chosen.Count == 3) break;
            }
            Assert.AreEqual(3, chosen.Count, "three carry spots in the field: " + string.Join(" ", TempleBooths.FieldSpots(spec, layout)));
            return chosen;
        }

        [UnityTest]
        public IEnumerator ThePhysicsTemple_IsSolvedWithTheRealKeys_AllThreeBouldersDraggedFromFieldToField()
        {
            yield return Load();
            yield return Rebuild(7, true);
            FaceId face = world.Model.FaceOf(Theme.Physics);
            yield return WalkIn(face);
            TempleView room = temples.Current;
            PhysicsTrial booths = room.Booths;
            MassMitt mitt = navigator.GetComponent<MassMitt>();
            for (int i = 0; i < booths.Doors.Count; i++)
            {
                TimedDoorGate door = booths.Doors[i];
                DoorLayout layout = room.Puzzle.Booths.Booths[i].Layout;
                List<Vector2> spots = CarrySpots(room.Puzzle.Booths, layout);
                // Nearest the lane last on the way out of a field: carry the boulders in their order, highest spot first.
                List<Boulder> order = booths.Boulders.OrderBy(b => b.Position.y).ToList();
                for (int b = 0; b < 3; b++) yield return Drag(room, mitt, order[b], spots[b]);
                yield return PhysicsSteps(2);
                Assert.AreEqual(3f, door.MassInField, 0.01f, $"booth {i}: all three dragged into its field");
                yield return ToSwitch(room, layout);
                yield return Dash(layout, door);
                Assert.IsTrue(door.IsOpen, $"booth {i}: opened by the dash");
            }
            Assert.IsTrue(room.IsSolved, "both booths: the temple is solved");
            yield return MagicAndGem(room, face, Theme.Physics);
        }

        // ---------------------------------------------------------------- the gem in the core

        [UnityTest]
        public IEnumerator TheGem_ChangesItsArenaParameter_AndNoGemsKeepTheBaseArena()
        {
            yield return Load();
            yield return Rebuild(7, true);
            FaceId face = world.Model.FaceOf(Theme.Math);
            world.UnsealTemple(face);
            Assert.AreEqual(BossBenefit.None, arena.RunBenefits, "an open temple gives nothing yet");
            Assert.IsTrue(arena.Enter(navigator));
            yield return PhysicsSteps(2);
            Assert.AreEqual(ArenaTuning.Base().GapHeight, arena.Tuning.GapHeight, 0.001f, "no gem: the base gap");
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(3);
            world.RepairFace(face); // the temple's gem
            Assert.AreEqual(BossBenefit.WiderGap, arena.RunBenefits);
            Assert.IsTrue(arena.Enter(navigator));
            yield return PhysicsSteps(2);
            Assert.AreEqual(ArenaTuning.WideGapHeight, arena.Tuning.GapHeight, 0.001f, "the gem widens the gaps");
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(3);
        }
    }
}
