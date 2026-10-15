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
    /// family is solved with the real keys: 4.19's Town sliding crest (tile by tile from the gap, a stuck tile first),
    /// Chemistry's crystals (the least strikes), Biology's finch song (round by round, a wrong note replaying it), the ice
    /// and the wind (the solver's route, move by move), the island
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
            yield return Swing();
        }

        /// <summary>
        /// The swing (once the last one's cooldown is over): Enter down for two frames, the keyboard's whole state in one
        /// event (as SetKeys moves).
        /// </summary>
        private IEnumerator Swing()
        {
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
            Assert.IsTrue(PickupPopup.Active.Any(p => p.Text == TempleText.Rule(TemplePuzzleKind.Sliding)), "the rule is said");

            // One slide, then out with S held: back in front of the door, facing away, staying put while S is held.
            TempleSlidingBoard tiles = room.Tiles;
            Assert.IsNotNull(tiles, "Town's temple: the sliding crest");
            List<int> solution = TempleSliding.Solution(tiles.Spec.Tiles);
            Assert.IsTrue(tiles.StrikeCell(solution[0]));
            Assert.AreEqual(1, tiles.Moves);
            CollectionAssert.AreNotEqual(tiles.Spec.Tiles.ToArray(), tiles.Tiles.ToArray(), "a tile slid");
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
            Assert.AreEqual(0, temples.Current.Tiles.Moves, "the puzzle reset");
            CollectionAssert.AreEqual(temples.Current.Tiles.Spec.Tiles.ToArray(), temples.Current.Tiles.Tiles.ToArray(), "the scramble as dealt");

            // A teleport out (F6) resets it too; solve it, and it stays solved.
            temples.Current.Tiles.StrikeCell(solution[0]);
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(3);
            Assert.IsNull(temples.Current);
            Assert.IsFalse(temples.Rooms.ContainsKey(town));
            Assert.IsTrue(temples.Enter(door));
            TempleView fresh = temples.Current;
            Assert.AreEqual(0, fresh.Tiles.Moves);
            foreach (int cell in solution) Assert.IsTrue(fresh.Tiles.StrikeCell(cell));
            Assert.IsTrue(fresh.IsSolved);
            Assert.IsTrue(fresh.Tiles.FinalPiece.enabled, "the last piece fills the gap");
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

        // ---------------------------------------------------------------- 4.19: the crest, the crystals and the finches, real keys

        /// <summary>
        /// 4.19 review: the rule popup of a board temple stands still and low, its text (about 0.6 units a line at 20 px) wholly
        /// below the lowest of the board's art (local y).
        /// </summary>
        private static void RuleStandsClear(TempleView room, float boardBottom)
        {
            PickupPopup rule = room.RulePopup;
            Assert.IsNotNull(rule, "the rule is said");
            Vector2 at = (Vector2)rule.Position - room.Centre;
            Assert.AreEqual(TempleView.BoardRuleSpot.y, at.y, 0.01f, "the rule stands low");
            float lines = rule.Text.Split('\n').Length;
            Assert.Less(at.y + lines * 0.6f / 2f + 0.15f, boardBottom, "its text stays below the board");
            Assert.Greater(at.y - lines * 0.6f / 2f, -ScreenEdges.Half.y, "and on the screen");
        }

        /// <summary>Strikes a tile beside the gap with the real keys: walks onto the gap, steps toward the tile (facing it), swings.</summary>
        private IEnumerator SlideTile(TempleView room, int cell)
        {
            TempleSlidingBoard tiles = room.Tiles;
            Vector2 gap = room.Centre + TempleSliding.CellLocal(tiles.Gap);
            Vector2 tile = room.Centre + TempleSliding.CellLocal(cell);
            yield return WalkToward(gap, 0.2f);
            Vector2 d = tile - gap;
            var dir = new Vector2Int(Mathf.Abs(d.x) > 0.1f ? Math.Sign(d.x) : 0, Mathf.Abs(d.y) > 0.1f ? Math.Sign(d.y) : 0);
            Vector2 from = body.position;
            SetKeys(keyboard, WasdKeys(dir));
            yield return WaitUntilOrGameTimeout(() => Vector2.Distance(body.position, from) > 0.3f, 1f);
            SetKeys(keyboard);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return Swing();
            yield return WaitUntilOrGameTimeout(() => false, TempleSlidingBoard.SlideSeconds + 0.05f);
        }

        [UnityTest]
        public IEnumerator TheTownTemple_SlidingCrest_IsSolvedWithTheRealKeys_TileByTile_AndAStuckTileStays()
        {
            yield return Load();
            yield return Rebuild(21, false);
            FaceId face = CubeModel.StartFace;
            yield return WalkIn(face);
            TempleView room = temples.Current;
            TempleSlidingBoard tiles = room.Tiles;
            Assert.IsNotNull(tiles, "Town's temple holds the sliding crest");
            Assert.That(tiles.Spec.OptimalMoves, Is.InRange(TempleSliding.MinMoves, TempleSliding.MaxMoves));
            RuleStandsClear(room, TempleSliding.BoardRect.yMin - 0.25f);

            // A tile not beside the gap: struck from beside it, it stays (and says so), no damage.
            float hearts = navigator.GetComponent<Health>().Current;
            int far = Enumerable.Range(0, TempleSliding.Cells).First(c => c != tiles.Gap && !TempleSliding.Adjacent(c, tiles.Gap) &&
                                                                          Enumerable.Range(0, 9).Any(n => n != tiles.Gap && TempleSliding.Adjacent(n, c)));
            int[] before = tiles.Tiles.ToArray();
            yield return StrikeTarget(room.Centre + TempleSliding.CellLocal(far));
            Assert.AreEqual(0, tiles.Moves, "a tile away from the gap never slides");
            Assert.AreEqual(1, tiles.StuckStrikes);
            CollectionAssert.AreEqual(before, tiles.Tiles.ToArray());
            Assert.IsTrue(PickupPopup.Active.Any(p => p != null && p.Text == TempleText.TileStuckPopup));
            Assert.AreEqual(hearts, navigator.GetComponent<Health>().Current, "no damage");

            var magicSeen = false;
            room.Solved += _ => magicSeen = true;
            List<int> solution = TempleSliding.Solution(tiles.Tiles);
            Assert.AreEqual(tiles.Spec.OptimalMoves, solution.Count);
            for (int i = 0; i < solution.Count; i++)
            {
                int gapBefore = tiles.Gap;
                yield return SlideTile(room, solution[i]);
                Assert.AreEqual(i + 1, tiles.Moves, $"slide {i + 1} of {solution.Count}: the struck tile slid");
                Assert.AreEqual(solution[i], tiles.Gap, "into the gap, which moved to where it was");
                Assert.AreNotEqual(gapBefore, tiles.Gap);
            }
            Assert.IsTrue(TempleSliding.IsSolved(tiles.Tiles));
            Assert.IsTrue(magicSeen && room.IsSolved, "the crest mended solves the temple");
            Assert.IsTrue(tiles.FinalPiece.enabled);
            yield return MagicAndGem(room, face, Theme.Town);
        }

        [UnityTest]
        public IEnumerator TheChemistryTemple_CrystalsAreLitWithTheRealKeys_EachStrikeFlippingItsNeighbours()
        {
            yield return Load();
            yield return Rebuild(21, true);
            FaceId face = world.Model.FaceOf(Theme.Chemistry);
            yield return WalkIn(face);
            TempleView room = temples.Current;
            TempleCrystals crystals = room.Crystals;
            Assert.IsNotNull(crystals, "Chemistry's temple holds the crystals");
            RuleStandsClear(room, TempleLights.CrystalLocal(0).y + 0.2f - 1.0f);
            List<int> solution = TempleLights.Solution(crystals.Board);
            Assert.That(solution.Count, Is.InRange(TempleLights.MinStrikes, TempleLights.MaxStrikes));
            var magicSeen = false;
            room.Solved += _ => magicSeen = true;
            for (int i = 0; i < solution.Count; i++)
            {
                int was = crystals.Board;
                yield return StrikeTarget(room.Centre + TempleLights.CrystalLocal(solution[i]));
                Assert.AreEqual(was ^ TempleLights.Flips(solution[i]), crystals.Board, $"strike {i + 1}: crystal {solution[i]} and its neighbours flipped");
                for (int c = 0; c < TempleLights.Count; c++)
                    Assert.AreEqual(crystals.IsLit(c) ? TempleArt.Crystal(true) : TempleArt.Crystal(false), crystals.Bodies[c].sprite, "drawn lit or dark");
            }
            Assert.AreEqual(TempleLights.AllLit, crystals.Board, "every crystal lit");
            Assert.IsTrue(magicSeen && room.IsSolved);
            yield return MagicAndGem(room, face, Theme.Chemistry);
        }

        [UnityTest]
        public IEnumerator TheBiologyTemple_FinchSongIsStruckBackWithTheRealKeys_AndAWrongNoteReplaysTheRound()
        {
            yield return Load();
            yield return Rebuild(21, true);
            FaceId face = world.Model.FaceOf(Theme.Biology);
            yield return WalkIn(face);
            TempleView room = temples.Current;
            TempleChoir choir = room.Choir;
            Assert.IsNotNull(choir, "Biology's temple holds the finches");
            IReadOnlyList<int> notes = choir.Spec.Notes;
            // 4.19 review: the rule stands low and still, clear of the finches; the song waits for it, or for a first step.
            RuleStandsClear(room, TempleSong.FinchLocal(2).y + 0.3f - 1.4f);
            yield return WaitUntilOrGameTimeout(() => false, 1f);
            Assert.AreEqual(TempleChoir.Phase.Waiting, choir.State, "no song while the rule is up and the player stands still");
            yield return WalkToward(room.Centre + TempleSong.Perch, 0.3f);
            yield return WaitUntilOrGameTimeout(() => choir.SingingNote >= 0, TempleSong.PauseSeconds + 1f);
            Assert.GreaterOrEqual(choir.SingingNote, 0, "a first step starts the song");
            int strikes = choir.Strikes;
            Assert.IsFalse(choir.StrikeFinch(notes[0]), "a strike while they sing is ignored");
            Assert.AreEqual(strikes, choir.Strikes, "and not counted");
            Assert.AreEqual(notes[0], choir.Statues.Select((st, i) => (st, i)).First(x => x.st.sprite == TempleArt.Finch(x.i, true)).i,
                "the first note's finch sings (its singing look)");
            yield return WaitUntilOrGameTimeout(() => choir.Listening, TempleSong.SongSeconds(TempleSong.Length) + 2f);
            Assert.IsTrue(choir.Listening, "the song ends: the player's turn");

            // A wrong note first: the round's song plays again, the round is kept, no damage.
            float hearts = navigator.GetComponent<Health>().Current;
            int wrong = (notes[0] + 1) % TempleSong.Finches;
            yield return StrikeTarget(room.Centre + TempleSong.FinchLocal(wrong));
            Assert.AreEqual(1, choir.Replays, "a wrong note");
            Assert.AreEqual(0, choir.Round, "the round is kept");
            Assert.AreEqual(0, choir.Progress);
            Assert.IsFalse(choir.Listening, "they sing it again");
            Assert.IsTrue(PickupPopup.Active.Any(p => p != null && p.Text == TempleText.WrongNotePopup));
            Assert.AreEqual(hearts, navigator.GetComponent<Health>().Current, "no damage");

            // The listening stone (real keys) sings the round's song again: no wrong note, the round kept.
            yield return WaitUntilOrGameTimeout(() => choir.Listening, TempleSong.SongSeconds(TempleSong.Length) + 2f);
            int sung = choir.SongsSung;
            yield return StrikeTarget(room.Centre + TempleSong.StoneSpot);
            Assert.AreEqual(1, choir.StoneReplays, "the stone sings it again");
            Assert.AreEqual(sung + 1, choir.SongsSung);
            Assert.AreEqual(1, choir.Replays, "not a wrong note");
            Assert.AreEqual(0, choir.Round);
            Assert.IsFalse(choir.Listening);

            var magicSeen = false;
            room.Solved += _ => magicSeen = true;
            for (int round = 0; round < TempleSong.Rounds; round++)
            {
                yield return WaitUntilOrGameTimeout(() => choir.Listening || choir.IsSolved, TempleSong.SongSeconds(TempleSong.Length) + 3f);
                Assert.IsTrue(choir.Listening, $"round {round + 1}: sung, then the player's turn");
                Assert.AreEqual(round, choir.Round);
                for (int n = 0; n < TempleSong.RoundLength(round); n++)
                {
                    yield return StrikeTarget(room.Centre + TempleSong.FinchLocal(notes[n]));
                    if (n < TempleSong.RoundLength(round) - 1) Assert.AreEqual(n + 1, choir.Progress, $"round {round + 1}, note {n + 1} struck back");
                }
            }
            Assert.IsTrue(choir.IsSolved, "the whole song struck back");
            Assert.AreEqual(1, choir.Replays, "no other wrong note");
            Assert.AreEqual(TempleSong.Rounds + 2, choir.SongsSung, "a song a round, the wrong note's replay and the stone's");
            Assert.IsTrue(magicSeen && room.IsSolved);
            yield return MagicAndGem(room, face, Theme.Biology);
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

        /// <summary>The key of a door's dash: from its stall toward its booth.</summary>
        private static Facing DashKey(DoorLayout layout) => TempleBooths.DashWay(layout).x < 0f ? Facing.West : Facing.East;

        /// <summary>
        /// Stands the player on a door's switch strip: set down just outside its stall's far end on the lane, then walked in
        /// with the real keys (toward the booth) until the strip is pressed, offset across the lane by across.
        /// </summary>
        private IEnumerator ToSwitch(TempleView room, DoorLayout layout, TimedDoorGate door, float across = 0f)
        {
            Rect stall = TempleBooths.Stall(layout);
            Vector2 way = TempleBooths.DashWay(layout);
            float outside = way.x < 0f ? stall.xMax + 0.9f : stall.xMin - 0.9f;
            yield return PutPlayerAt(room.Centre + new Vector2(outside, layout.DoorLocal.y + across));
            yield return PhysicsSteps(2);
            int presses = door.Switch.Presses;
            yield return this.HoldUntil(KeyFor(keyboard, DashKey(layout)), () => door.Switch.Presses > presses, 3f);
            yield return PhysicsSteps(2);
            Assert.Greater(door.Switch.Presses, presses, "walked onto the switch strip");
            Assert.IsTrue(door.IsAjar, "and it swings the door open");
        }

        /// <summary>The dash, with the real keys: held toward the booth until the player gets in (or the time is long up).</summary>
        private IEnumerator Dash(DoorLayout layout, TimedDoorGate door, float seconds = 4f)
        {
            int passes = door.Passes;
            yield return this.HoldUntil(KeyFor(keyboard, DashKey(layout)), () => door.Passes > passes, seconds);
            yield return PhysicsSteps(2);
        }

        /// <summary>Walks back out of a booth with the real keys and waits for its door to shut on its timer (4.25: no latch).</summary>
        private IEnumerator OutAndShut(DoorLayout layout, TimedDoorGate door)
        {
            int closings = door.Closings;
            Facing back = DashKey(layout) == Facing.West ? Facing.East : Facing.West;
            yield return this.HoldUntil(KeyFor(keyboard, back), () => !door.IsHeld, 3f);
            yield return this.HoldUntil(KeyFor(keyboard, back), () => false, 0.4f);
            float end = Time.time + 6f;
            while (door.Closings == closings && Time.time < end) yield return null;
            Assert.Greater(door.Closings, closings, "the door times out once the player has left");
            Assert.IsFalse(door.IsOpen, "it never latches open");
            Assert.IsTrue(door.Solid.enabled, "and it is shut again");
        }

        /// <summary>Three boulders in a door's field, set down at spots off every dash.</summary>
        private IEnumerator ThreeInTheField(TempleView room, PhysicsTrial booths, DoorLayout layout, int count = 3)
        {
            List<Vector2> spots = TempleBooths.FieldSpots(room.Puzzle.Booths, layout);
            for (int b = 0; b < booths.Boulders.Count; b++)
                booths.Boulders[b].Teleport(b < count ? room.Centre + spots[b * spots.Count / 3] : room.Centre + new Vector2(0f, -5f));
            yield return PhysicsSteps(2);
        }

        [UnityTest]
        public IEnumerator ThePhysicsTemple_LendsItsGrip_EachBoothCountsOnlyWhenRunIntoInTime_AndItsDoorTimesOutAfter()
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
            Assert.IsTrue(booths.TempleRules, "the temple's doors time out instead of latching");
            Assert.IsTrue(mitt.TempleGrip && mitt.IsEquipped, "the temple lends its grip");
            Assert.AreEqual(MassMitt.TempleGrabText, mitt.HudText, "the HUD says how to grab, not the mitt's name");
            Assert.AreEqual(4, room.StallPosts.Count, "two stall posts per door");
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
                Assert.AreEqual(TempleBooths.FieldK, door.DilationPerBoulder, 1e-4f, "the temple's stronger field");
                Assert.IsFalse(door.Latches);
                Assert.AreEqual(3f, door.MinMass, 1e-4f, "short of all three");
                Assert.AreEqual(TempleBooths.ShortRush, door.ShortRush, 1e-4f, "its clock rushes");
                yield return ThreeInTheField(room, booths, layout);
                Assert.AreEqual(3f, door.MassInField, 0.01f, "three boulders in the field");
                Assert.GreaterOrEqual(door.OpenSecondsNow, 2.25f * TempleBooths.DashSeconds(layout) - 1e-3f, "open well past the dash");
                yield return ToSwitch(room, layout, door);
                yield return Dash(layout, door);
                Assert.AreEqual(1, door.Passes, $"booth {i}: run into while open");
                Assert.IsTrue(booths.IsBoothDone(i), $"booth {i}: done");
                Assert.IsFalse(door.IsOpen, "never latched");
                Assert.IsTrue(door.IsAjar, "held open while the player stands in it");
                if (i == 0)
                {
                    // 4.25 review: well inside the booth, idle (the body asleep, so no trigger Stay arrives) for longer than
                    // the door's time: it never shuts on the player, who then walks out.
                    yield return this.HoldUntil(KeyFor(keyboard, DashKey(layout)), () => false, 0.3f);
                    yield return PhysicsSteps(2);
                    body.Sleep();
                    yield return new WaitForSeconds(4.5f);
                    Assert.Greater(4.5f, door.OpenSecondsNow, "idled longer than the door's open time");
                    Assert.IsTrue(door.IsAjar, "still open with the player idle inside");
                    Assert.IsFalse(door.Solid.enabled, "and not solid");
                    Assert.IsTrue(door.PlayerInDoorway());
                }
                yield return OutAndShut(layout, door);
            }
            Assert.IsTrue(booths.IsComplete);
            Assert.IsTrue(room.IsSolved);
            yield return MagicAndGem(room, face, Theme.Physics);
            Assert.IsTrue(temples.Leave());
            Assert.IsFalse(mitt.TempleGrip, "the grip stays in the temple");
            Assert.IsNull(mitt.HudText, "and so does its HUD line");
        }

        [UnityTest]
        public IEnumerator APhysicsDash_FromTheStripsEdge_StillMakesIt_WithThreeBoulders()
        {
            // Standing on the strip's edge (as far across the lane as the stall lets a player stand), the straight dash is just
            // as long and still makes it in time.
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
                yield return ThreeInTheField(room, booths, layout);
                float across = (i == 0 ? 1f : -1f) * (TempleBooths.StallInner / 2f - TimeField.PlayerRadius - 0.02f);
                yield return ToSwitch(room, layout, door, across);
                yield return Dash(layout, door);
                Assert.AreEqual(1, door.Passes, $"booth {i}: a dash {across:0.00} off the lane's middle still gets in");
                yield return OutAndShut(layout, door);
            }
            Assert.IsTrue(room.IsSolved);
        }

        [UnityTest]
        public IEnumerator APhysicsBooth_ShutsBeforeTheDashEnds_WithOnlyTwoBoulders()
        {
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
                yield return ThreeInTheField(room, booths, layout, 2);
                Assert.AreEqual(2f, door.MassInField, 0.01f);
                yield return ToSwitch(room, layout, door);
                yield return Dash(layout, door, 3f);
                Assert.AreEqual(0, door.Passes, $"booth {i}: one boulder short, shut before the dash ends");
                Assert.GreaterOrEqual(door.Closings, 1);
                Assert.IsFalse(booths.IsBoothDone(i));
                Assert.IsTrue(door.Solid.enabled, "the door is shut in the player's face");
            }
            Assert.IsFalse(room.IsSolved);
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

        /// <summary>The players' lane along the bottom of the room (local y), under every boulder and dash.</summary>
        private const float Lane = -6.4f;

        /// <summary>
        /// Three spots in a field a boulder can be carried up to from the bottom lane: columns apart, with nothing solid (a booth
        /// or a stall post) between the lane and the spot.
        /// </summary>
        private static List<Vector2> CarrySpots(PhysicsTrialSpec spec, DoorLayout layout)
        {
            var solids = new List<Rect>();
            foreach (TrialBoothSpec b in spec.Booths)
            {
                solids.Add(b.Footprint);
                solids.AddRange(TempleBooths.StallPosts(b.Layout));
            }
            var chosen = new List<Vector2>();
            foreach (Vector2 p in TempleBooths.FieldSpots(spec, layout).OrderByDescending(p => p.y))
            {
                bool blocked = solids.Any(r => r.xMax > p.x - 1.4f && r.xMin < p.x + 1.4f && r.yMin < p.y + 0.7f);
                if (blocked || p.y < Lane + 1.6f || chosen.Any(q => Vector2.Distance(q, p) < 1.3f)) continue;
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
                yield return ToSwitch(room, layout, door);
                yield return Dash(layout, door);
                Assert.AreEqual(1, door.Passes, $"booth {i}: got in on the dash");
                yield return OutAndShut(layout, door);
            }
            Assert.IsTrue(room.IsSolved, "both booths: the temple is solved");
            yield return MagicAndGem(room, face, Theme.Physics);
        }

        // ---------------------------------------------------------------- 4.19: a held boulder drops on E, whichever way

        [UnityTest]
        public IEnumerator AHeldBoulder_PinnedOverThePlayer_DropsOnInteract_InEveryFacing_AtAFreeSpot_EvenWithAPersonInTalkingRange()
        {
            yield return Load();
            yield return Rebuild(7, true);
            FaceId face = world.Model.FaceOf(Theme.Physics);
            MassMitt mitt = navigator.GetComponent<MassMitt>();
            yield return WalkIn(face);
            TempleView room = temples.Current;
            Boulder boulder = room.Booths.Boulders[0];
            // The middle of the floor, clear of the booths and the other boulders.
            room.Booths.Boulders[1].Teleport(room.Centre + new Vector2(-5f, 5.5f));
            room.Booths.Boulders[2].Teleport(room.Centre + new Vector2(5f, 5.5f));
            Vector2 middle = room.Centre + new Vector2(0f, -4.2f); // 4.25: below the booths and their dashes, clear of the doorway
            var ways = new[]
            {
                new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1),
                new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 0), new Vector2Int(-1, 1),
            };
            Rect bounds = boulder.Bounds.Value;
            foreach (Vector2Int way in ways)
            {
                Vector2 dir = ((Vector2)way).normalized;
                boulder.Teleport(middle + dir * 1.5f * K);
                yield return PutPlayerAt(middle);
                yield return PhysicsSteps(2);
                yield return this.Tap(keyboard.eKey, 2);
                Assert.AreSame(boulder, mitt.Held, $"{way}: grabbed");
                // Turn to face the way (a short walk that way: the boulder comes along).
                SetKeys(keyboard, WasdKeys(way));
                yield return WaitUntilOrGameTimeout(() => false, 0.12f);
                SetKeys(keyboard);
                yield return PhysicsSteps(2);
                Assert.AreEqual(dir.x, mover.Facing.x, 0.05f, $"{way}: facing that way");
                Assert.AreEqual(dir.y, mover.Facing.y, 0.05f);
                // Pinned: the boulder at the edge (or corner) of its bounds that way, half over the player (a held boulder
                // ignores the player, and its bounds keep it from moving on), so where it stands is no place to drop it.
                var pinned = new Vector2(way.x > 0 ? bounds.xMax : way.x < 0 ? bounds.xMin : middle.x,
                    way.y > 0 ? bounds.yMax : way.y < 0 ? bounds.yMin : middle.y);
                boulder.Body.position = pinned;
                boulder.transform.position = pinned;
                body.position = pinned - dir * 0.9f;
                body.linearVelocity = Vector2.zero;
                navigator.transform.position = body.position;
                Physics2D.SyncTransforms();
                yield return PhysicsSteps(1);
                Assert.AreSame(boulder, mitt.Held, $"{way}: still held, pinned");
                Assert.Less(Vector2.Distance(boulder.Position, body.position), 2f * TimeField.PlayerRadius, $"{way}: over the player");
                Assert.IsFalse(MassMitt.FreeForBoulder(boulder, boulder.Position), $"{way}: its own spot is not free");
                yield return this.Tap(keyboard.eKey, 2);
                Assert.IsNull(mitt.Held, $"{way}: E drops it, whichever way the player faces");
                Assert.AreEqual(mitt.LastDropSpot, boulder.Position, $"{way}: at the drop spot");
                Assert.IsTrue(MassMitt.FreeForBoulder(boulder, boulder.Position), $"{way}: set down where nothing solid is, off the player");
                Assert.GreaterOrEqual(Vector2.Distance(boulder.Position, body.position), 2f * TimeField.PlayerRadius - 0.05f, $"{way}: beside the player");
                Assert.LessOrEqual(Vector2.Distance(boulder.Position, body.position), MassMitt.DropReach + 0.01f, $"{way}: round the player");
                Assert.IsTrue(bounds.Contains(boulder.Position), $"{way}: inside its room");
            }

            // A swing drops it the same way (4.19 review).
            boulder.Teleport(middle + Vector2.up * 1.5f * K);
            yield return PutPlayerAt(middle);
            yield return PhysicsSteps(2);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.AreSame(boulder, mitt.Held);
            Assert.IsTrue(mitt.SwingAt(boulder, null), "a swing at the held boulder");
            Assert.IsNull(mitt.Held, "drops it");
            Assert.AreEqual(mitt.LastDropSpot, boulder.Position);
            Assert.IsTrue(MassMitt.FreeForBoulder(boulder, boulder.Position));

            // A person in talking range: E still drops the boulder and opens no talk.
            yield return PutPlayerAt(middle);
            yield return PhysicsSteps(2);
            boulder.Teleport(middle + Vector2.up * 1.5f * K);
            yield return PhysicsSteps(2);
            yield return this.Tap(keyboard.eKey, 2);
            Assert.AreSame(boulder, mitt.Held);
            CubeDebug debug = Object.FindAnyObjectByType<CubeDebug>();
            debug.SpawnNpcs(false);
            yield return PhysicsSteps(2);
            NpcTalker near = debug.DebugNpcs.First();
            near.transform.position = body.position + Vector2.left * 1.2f * K;
            yield return PhysicsSteps(2);
            Assert.IsTrue(near.PlayerInRange(), "a person in talking range");
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsNull(mitt.Held, "the boulder dropped");
            Assert.IsFalse(DialogueBox.Shared != null && DialogueBox.Shared.IsOpen, "and no talk opened");
            yield return this.Tap(keyboard.eKey, 2);
            Assert.IsTrue(mitt.Held != null || (DialogueBox.Shared != null && DialogueBox.Shared.IsOpen),
                "with empty hands, E grabs or talks as before");
        }

        // ---------------------------------------------------------------- 4.19 review: the overlay's real lines fit

        [UnityTest]
        public IEnumerator TheOverlaysRealLines_FitTheirBars_At960x600_Everywhere()
        {
            yield return Load();
            yield return Rebuild(7, true);
            CubeDebug debug = Object.FindAnyObjectByType<CubeDebug>();
            Assert.Greater(CubeDebug.MeasuredWidth(CubeDebug.KeysLine, 12), 100f, "the built-in font measures text");
            var lines = new List<string>();
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(2);
            lines.Add(debug.LocationLine);
            foreach (FaceId face in TemplePlan.TempleFaces(world.Model))
                for (int y = 0; y < world.FaceSize; y++)
                    for (int x = 0; x < world.FaceSize; x++)
                    {
                        navigator.TeleportTo(new ScreenAddress(face, x, y), Facing.North);
                        yield return null;
                        lines.Add(debug.LocationLine);
                    }
            yield return WalkIn(world.Model.FaceOf(Theme.Physics));
            lines.Add(debug.LocationLine);
            Assert.IsTrue(temples.Leave());
            yield return PhysicsSteps(2);
            Assert.IsTrue(arena.Enter(navigator));
            yield return PhysicsSteps(2);
            lines.Add(debug.LocationLine);
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(2);
            foreach (string line in lines)
            {
                StringAssert.StartsWith("Seed 7", line);
                int size = CubeDebug.FittedFontSize(line, 960f, 600f);
                Assert.GreaterOrEqual(size, 11, $"readable at 11 px or more: {line}");
                Assert.LessOrEqual(CubeDebug.MeasuredWidth(line, size), 960f - 16f, line);
            }
            int keys = CubeDebug.FittedFontSize(CubeDebug.KeysLine, 960f, 600f);
            Assert.GreaterOrEqual(keys, 11, "every key at 11 px or more");
            Assert.LessOrEqual(CubeDebug.MeasuredWidth(CubeDebug.KeysLine, keys), 960f - 16f);
            // Every race hostile: the longest location line still fits, at 10 px at least.
            foreach (Race race in RaceExtensions.All) world.Relations.SetHostile(race, true);
            string longest = debug.LocationLine;
            foreach (Race race in RaceExtensions.All) world.Relations.SetHostile(race, false);
            int fitted = CubeDebug.FittedFontSize(longest, 960f, 600f);
            Assert.LessOrEqual(CubeDebug.MeasuredWidth(longest, fitted), 960f - 16f, longest);
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
