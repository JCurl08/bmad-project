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
    /// Playtest 4 polish (4.18) in the real Cube scene:
    /// <list type="bullet">
    /// <item>the dialogue freeze: while a conversation is open the player cannot move, swing or switch items, every other NPC
    /// and enemy holds still, a timed door's timer holds, and in the arena the Demon, its motes and its shots hold; the box
    /// still works, and everything moves again once it closes;</item>
    /// <item>the quest log: Q opens it (pausing like the map, and from the map), and every quest given joins it: a leader's
    /// (with live fragment progress), a face's errand (done when carried), Town's side quest;</item>
    /// <item>Town's lost-and-found side quest, walked with real presses on several seeds: the Mayor, each townsperson in turn,
    /// the favour, the fragment, and the Mayor mending Town.</item>
    /// </list>
    /// </summary>
    public class QuestSceneTests : InputTestFixture
    {
        private const float K = ScreenMath.ContentScale;
        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private RunRewards rewards;
        private Rigidbody2D body;
        private QuestLogView questLog;
        private CubeMap map;
        private TownPopulation town;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            DialogueBox box = Object.FindAnyObjectByType<DialogueBox>();
            if (box != null) box.Close();
            GamePause.Release(QuestLogView.PauseKey);
            GamePause.Resume();
            Time.timeScale = 1f;
            base.TearDown();
        }

        private IEnumerator Load(int seed)
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            rewards = navigator.GetComponent<RunRewards>();
            body = navigator.GetComponent<Rigidbody2D>();
            questLog = Object.FindAnyObjectByType<QuestLogView>();
            map = Object.FindAnyObjectByType<CubeMap>();
            town = Object.FindAnyObjectByType<TownPopulation>();
            Assert.IsNotNull(questLog, "The Cube scene has the quest log");
            Assert.AreSame(world, questLog.World);
            Assert.AreSame(map, questLog.Map);
            world.Rebuild(seed);
            yield return null;
            yield return null;
            Assert.AreEqual(seed, town.PopulatedSeed);
        }

        private void FaceEnemies(bool on)
        {
            world.GetComponent<BiologyFace>().SpawnEnemies = on;
            world.GetComponent<ChemistryFace>().SpawnEnemies = on;
            world.GetComponent<PhysicsFace>().SpawnEnemies = on;
            world.GetComponent<MathFace>().SpawnEnemies = on;
            world.GetComponent<EarthFace>().SpawnEnemies = on;
        }

        private void PlacePlayer(Vector2 at)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
        }

        private ScreenAddress ScreenOf(Vector2 position)
        {
            Assert.IsTrue(world.TryScreenAt(position, out ScreenAddress screen), $"{position} is on a screen");
            return screen;
        }

        /// <summary>
        /// Walks up to an NPC (a teleport to its screen, then right beside it) and talks with a real Interact press, then
        /// presses through to the end; returns what was said.
        /// </summary>
        private IEnumerator TalkTo(NpcTalker npc, List<string> said)
        {
            said.Clear();
            DialogueBox box = npc.Box;
            Assert.IsFalse(box.IsOpen, "no conversation is open");
            ScreenAddress screen = ScreenOf(npc.transform.position);
            if (navigator.Current != screen || navigator.InInterior) navigator.TeleportTo(screen, Facing.East);
            yield return null;
            PlacePlayer((Vector2)npc.transform.position + new Vector2(0.3f, 0f) * K);
            yield return null;
            yield return this.Tap(keyboard.eKey);
            Assert.IsTrue(box.IsOpen, $"{npc.Spec.DisplayName} talks");
            Assert.AreSame(npc, box.Owner);
            said.AddRange(box.Lines);
            Assert.IsTrue(GamePause.Talking, "a conversation holds the talk pause");
            Assert.AreEqual(0f, Time.timeScale, "and time stands still while it is open");
            for (int i = 0; i < 6 && box.IsOpen; i++) yield return this.Tap(keyboard.eKey);
            Assert.IsFalse(box.IsOpen, "pressing through closes it");
            Assert.IsFalse(GamePause.Talking);
            Assert.AreEqual(1f, Time.timeScale, "time comes back");
            yield return null;
        }

        // ---------- The dialogue freeze ----------

        [UnityTest]
        public IEnumerator Talking_FreezesThePlayer_AndEveryoneElse_UntilTheConversationEnds()
        {
            yield return Load(7);
            FaceEnemies(false);
            var attack = navigator.GetComponent<PlayerAttack>();
            var equipment = navigator.GetComponent<Equipment>();
            var inventory = navigator.GetComponent<Inventory>();
            inventory.Add(world.ItemFor(Theme.Biology));
            inventory.Add(world.ItemFor(Theme.Chemistry));
            yield return null;
            ItemDefinition equipped = equipment.Equipped;
            Assert.IsNotNull(equipped);

            NpcTalker greeter = town.Npcs[0];
            Assert.IsTrue(TownPlan.IsGreeter(greeter.Spec));
            navigator.TeleportTo(ScreenOf(greeter.transform.position), Facing.East);
            yield return null;
            PlacePlayer((Vector2)greeter.transform.position + new Vector2(0.3f, 0f) * K);
            yield return null;
            yield return this.Tap(keyboard.eKey);
            DialogueBox box = greeter.Box;
            Assert.IsTrue(box.IsOpen, "the greeter talks");
            Assert.IsTrue(GamePause.Talking);
            Assert.IsFalse(GamePause.Active, "a talk is not a menu");
            Assert.AreEqual(0f, Time.timeScale);

            // Everyone's place, then the player tries to walk, swing and switch items for half a second.
            Vector2 player = body.position;
            var people = Object.FindObjectsByType<NpcMover>(FindObjectsSortMode.None)
                .ToDictionary(m => m, m => m.GetComponent<Rigidbody2D>().position);
            float fixedTime = Time.fixedTime, gameTime = Time.time;
            int swings = attack.SwingCount;
            Press(keyboard.dKey, queueEventOnly: true);
            yield return WaitRealSeconds(0.5f);
            Release(keyboard.dKey, queueEventOnly: true);
            yield return this.Tap(keyboard.enterKey, 2);
            yield return this.Tap(keyboard.digit2Key, 2);
            yield return this.Tap(keyboard.qKey, 2);
            Assert.IsFalse(questLog.IsOpen, "Q does nothing while talking");
            Assert.IsTrue(box.IsOpen, "walking, swinging, switching and Q never close the conversation");
            Assert.AreEqual(player, body.position, "the player cannot move");
            Assert.AreEqual(swings, attack.SwingCount, "or swing");
            Assert.AreSame(equipped, equipment.Equipped, "or switch items");
            Assert.AreEqual(fixedTime, Time.fixedTime, "no physics step runs: every timer, driver, NPC and enemy holds");
            Assert.AreEqual(gameTime, Time.time, "game time stands still");
            foreach (KeyValuePair<NpcMover, Vector2> p in people)
                Assert.AreEqual(p.Value, p.Key.GetComponent<Rigidbody2D>().position, $"{p.Key.name} holds still");

            // The box still works: Interact pages through and closes it, and the same press never swings.
            for (int i = 0; i < 6 && box.IsOpen; i++) yield return this.Tap(keyboard.eKey);
            Assert.IsFalse(box.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(swings, attack.SwingCount);
            Vector2 before = navigator.transform.position;
            Press(keyboard.dKey, queueEventOnly: true); // away from the greeter
            yield return WaitRealSeconds(0.3f);
            Release(keyboard.dKey, queueEventOnly: true);
            yield return null;
            Assert.Greater(((Vector2)navigator.transform.position - before).magnitude, 0.1f * K, "movement resumes after the talk");
            Assert.Greater(Time.fixedTime, fixedTime, "and so does the world");
        }

        [UnityTest]
        public IEnumerator Talking_HoldsEnemies_TimedDoors_AndTheArena()
        {
            yield return Load(21);
            FaceEnemies(true);
            Assert.IsTrue(world.RevealScience());
            PhysicsFace physics = world.GetComponent<PhysicsFace>();
            Assert.Greater(physics.Doors.Count, 0, "seed 21 has timed doors");
            TimedDoorSpec spec = physics.Plan.Doors[0];
            TimedDoorGate door = physics.DoorFor(spec);
            navigator.TeleportTo(spec.Screen, Facing.East);
            yield return PhysicsSteps(3);
            door.Trigger();
            yield return PhysicsSteps(10);
            Assert.IsTrue(door.IsAjar, "the door swings open on its timer");
            float left = door.LocalSecondsLeft;
            List<Enemy> enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Where(e => e.isActiveAndEnabled).ToList();
            Assert.Greater(enemies.Count, 0, "the faces have enemies");
            var places = enemies.ToDictionary(e => e, e => (Vector2)e.transform.position);

            DialogueBox box = DialogueBox.Shared;
            Assert.IsTrue(box.Open("Someone", new[] { "a line", "another" }));
            yield return WaitRealSeconds(0.6f);
            Assert.AreEqual(left, door.LocalSecondsLeft, "the timed door's timer holds");
            Assert.IsTrue(door.IsAjar);
            foreach (KeyValuePair<Enemy, Vector2> e in places)
                Assert.AreEqual(e.Value, (Vector2)e.Key.transform.position, $"{e.Key.name} holds still");
            box.Close();
            yield return PhysicsSteps(10);
            Assert.Less(door.LocalSecondsLeft, left, "the timer runs again once the talk ends");

            // The arena: the Demon, its motes and its shots all hold.
            CoreArena arena = Object.FindAnyObjectByType<CoreArena>();
            Assert.IsTrue(arena.Enter(navigator));
            yield return PhysicsSteps(30);
            MaxwellDemon demon = arena.Demon;
            Vector2 demonAt = demon.transform.position;
            Dictionary<Particle, Vector2> motes = arena.Particles.Where(p => p != null).ToDictionary(p => p, p => (Vector2)p.transform.position);
            var shots = Object.FindObjectsByType<OrderPulse>(FindObjectsSortMode.None).ToDictionary(s => s, s => (Vector2)s.transform.position);
            int fired = demon.PulsesFired;
            Assert.IsTrue(box.Open("Someone", new[] { "a line" }));
            yield return WaitRealSeconds(0.6f);
            Assert.AreEqual(demonAt, (Vector2)demon.transform.position, "the Demon holds");
            foreach (KeyValuePair<Particle, Vector2> m in motes)
                Assert.AreEqual(m.Value, (Vector2)m.Key.transform.position, "every mote holds");
            foreach (KeyValuePair<OrderPulse, Vector2> s in shots)
                if (s.Key != null) Assert.AreEqual(s.Value, (Vector2)s.Key.transform.position, "every shot holds");
            Assert.AreEqual(fired, demon.PulsesFired, "and it fires nothing");
            box.Close();
            yield return PhysicsSteps(20);
            Assert.IsTrue(motes.Any(m => m.Key != null && ((Vector2)m.Key.transform.position - m.Value).sqrMagnitude > 1e-6f),
                "the motes move again once the talk ends");
        }

        // ---------- The quest log ----------

        [UnityTest]
        public IEnumerator QuestLog_OpensWithQ_AndFromTheMap_PausingTheGame()
        {
            yield return Load(7);
            FaceEnemies(false);
            Assert.IsFalse(questLog.IsOpen);
            yield return this.Tap(keyboard.qKey, 2);
            Assert.IsTrue(questLog.IsOpen, "Q opens the quest log");
            Assert.IsTrue(GamePause.Active, "it pauses like the map");
            Assert.AreEqual(0f, Time.timeScale);
            yield return this.Tap(keyboard.qKey, 2);
            Assert.IsFalse(questLog.IsOpen, "Q again closes it");
            Assert.AreEqual(1f, Time.timeScale);

            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsTrue(map.IsOpen);
            yield return this.Tap(keyboard.qKey, 2);
            Assert.IsTrue(questLog.IsOpen, "Q on the map opens the log");
            Assert.IsFalse(map.IsOpen, "in the map's place");
            Assert.AreEqual(0f, Time.timeScale);
            yield return this.Tap(keyboard.mKey, 2);
            Assert.IsTrue(map.IsOpen, "M from the log opens the map");
            Assert.IsFalse(questLog.IsOpen);
            yield return this.Tap(keyboard.escapeKey, 2);
            Assert.IsFalse(map.IsOpen);
            Assert.IsFalse(GamePause.Active);
            Assert.AreEqual(1f, Time.timeScale, "the game resumes");

            yield return this.Tap(keyboard.qKey, 2);
            Assert.IsTrue(questLog.IsOpen);
            yield return this.Tap(keyboard.escapeKey, 2);
            Assert.IsFalse(questLog.IsOpen, "Escape closes it");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator EveryQuestGiven_JoinsTheLog_UpdatesWithProgress_AndIsMarkedDone()
        {
            yield return Load(21);
            FaceEnemies(false);
            var said = new List<string>();
            Assert.AreEqual(0, world.Quests.Entries.Count, "a new run's log is empty");

            // Town's errand: one visitor on the square gives it, another receives it.
            ErrandPlan errand = town.Errand;
            Assert.IsNotNull(errand, "Town has its errand");
            NpcTalker giver = town.NpcAt(errand.GiverIndex + 1), target = town.NpcAt(errand.TargetIndex + 1);
            yield return TalkTo(target, said);
            Assert.IsFalse(world.Quests.Has(QuestLog.ErrandId(CubeModel.StartFace)), "nothing to deliver before the errand is given");
            yield return TalkTo(giver, said);
            Assert.AreEqual(Dialogue.ErrandLine(errand.Giver, errand.Target.Name), said[0], "the giver gives the errand");
            QuestEntry entry = world.Quests.Get(QuestLog.ErrandId(CubeModel.StartFace));
            Assert.IsNotNull(entry, "the errand joins the log");
            Assert.AreEqual(QuestKind.Errand, entry.Kind);
            Assert.IsFalse(entry.Done);
            StringAssert.Contains(errand.Target.Name, entry.Step);
            Assert.IsTrue(PickupPopup.Active.Any(p => p.Text == QuestText.Added(entry.Title)), "with a popup");
            yield return TalkTo(target, said);
            Assert.AreEqual(QuestLines.DeliveredLine(errand.Target.Voice, errand.Giver.Name), said[0], "the word arrives");
            Assert.IsTrue(entry.Done, "the errand is done");
            yield return TalkTo(giver, said);
            Assert.AreEqual(QuestLines.ThanksLine(errand.Giver.Voice), said[0], "and its giver thanks the player");

            // Town's side quest joins the log with the Mayor's first talk.
            yield return TalkTo(town.Leader, said);
            QuestEntry side = world.Quests.Get(QuestLog.TownId);
            Assert.IsNotNull(side);
            Assert.AreEqual(QuestKind.TownSide, side.Kind);

            // A science leader's quest, with live progress, then done on the hand-in.
            Assert.IsTrue(world.RevealScience());
            yield return null;
            yield return null;
            FaceId face = world.Model.FaceOf(Theme.Biology);
            int count = world.Dice.Of(face);
            NpcTalker leader = world.GetComponent<TownSquares>().LeaderOf(face);
            yield return TalkTo(leader, said);
            QuestEntry quest = world.Quests.Get(QuestLog.LeaderId(face));
            Assert.IsNotNull(quest, "the leader's quest joins the log");
            Assert.AreEqual(QuestText.FragmentProgress(0, count), quest.Progress);
            rewards.Grant(Reward.Fragment(1), face);
            yield return this.Tap(keyboard.qKey, 2);
            Assert.IsTrue(questLog.IsOpen);
            Assert.AreEqual(QuestText.FragmentProgress(1, count), quest.Progress, "the log shows live progress");
            yield return this.Tap(keyboard.qKey, 2);
            for (int i = 2; i <= count; i++) rewards.Grant(Reward.Fragment(i), face);
            yield return TalkTo(world.GetComponent<TownSquares>().LeaderOf(face), said);
            Assert.IsTrue(world.Repairs.IsUnsealed(face), "the hand-in opens the temple (4.14)");
            Assert.IsFalse(world.Repairs.IsRepaired(face));
            Assert.IsTrue(quest.Done, "the hand-in marks it done");
            Assert.AreEqual(QuestText.LeaderDone(Theme.Biology, leader.Spec.Name), quest.Step);
            QuestEntry temple = world.Quests.Get(QuestLog.TempleId(face));
            Assert.IsNotNull(temple, "the temple's quest joins the log on the hand-in");
            Assert.IsFalse(temple.Done);
            Assert.AreEqual(QuestText.TempleStep(Theme.Biology), temple.Step);
            world.RepairFace(face);
            world.Quests.RefreshLeaders(world, rewards);
            Assert.IsTrue(temple.Done, "done once its gem is won");

            // The face's errand too.
            ErrandPlan faceErrand = world.GetComponent<TownSquares>().FaceErrands[face];
            NpcTalker faceGiver = world.GetComponent<TownSquares>().Residents.First(r => r != null && r.GetComponent<ErrandQuest>() != null && r.GetComponent<ErrandQuest>().IsGiver && r.GetComponent<ErrandQuest>().Plan.Face == face);
            NpcTalker faceTarget = world.GetComponent<TownSquares>().Residents.First(r => r != null && r.GetComponent<ErrandQuest>() != null && !r.GetComponent<ErrandQuest>().IsGiver && r.GetComponent<ErrandQuest>().Plan.Face == face);
            yield return TalkTo(faceGiver, said);
            yield return TalkTo(faceTarget, said);
            Assert.IsTrue(world.Quests.IsDone(QuestLog.ErrandId(face)), $"{faceErrand}: carried");

            List<QuestEntry> shown = world.Quests.Ordered();
            Assert.AreEqual(5, shown.Count, "Town's errand and side quest, the leader's quest, its temple's (4.14) and the face's errand");
            Assert.AreEqual(QuestLog.TownId, shown[0].Id, "the open quest first");
            Assert.IsTrue(shown.Skip(1).All(e => e.Done));

            // A new run starts a fresh log.
            world.Rebuild(22);
            yield return null;
            yield return null;
            Assert.AreEqual(0, world.Quests.Entries.Count);
        }

        // ---------- What the spawned people say, seeds 1-50 ----------

        /// <summary>Each voice's errand without its name: the words after the target's name.</summary>
        private static List<string> ErrandStems() => Voices.All
            .Select(v => Dialogue.Flavours[v].Errand)
            .Select(e => e.Substring(e.IndexOf("{name}", System.StringComparison.Ordinal) + "{name}".Length))
            .ToList();

        [UnityTest]
        public IEnumerator OverSeeds1To50_TheSpawnedTown_IsThePureTown_TheTrailIsReady_AndEachFaceHasOneErrandGiver()
        {
            yield return Load(1);
            FaceEnemies(false);
            TownSquares squares = world.GetComponent<TownSquares>();
            TownInteriors interiors = world.GetComponent<TownInteriors>();
            List<string> stems = ErrandStems();
            var problems = new List<string>();
            for (int seed = 1; seed <= 50; seed++)
            {
                world.Rebuild(seed);
                yield return null;
                yield return null;
                int n = world.FaceSize;
                // The sweep's pure spots are the spawned ones, so the trail the sweep proves is the one in play.
                List<TownSpot> pure = TownPlan.Spots(seed, n, TownPlan.Specs(seed, n).Count,
                    cell => TownPlan.BlockedRects(world.Library.TownModule(cell, n)));
                if (!pure.SequenceEqual(town.Spots)) problems.Add($"seed {seed}: the spawned Town spots are not the pure ones");
                if (!town.Trail.Ready) problems.Add($"seed {seed}: the trail is not ready");
                if (town.Errand == null) problems.Add($"seed {seed}: Town has no errand");
                foreach (int index in world.TownTrail.Steps.Append(world.TownTrail.Helper))
                    if (!town.SpotAt(index).Equals(pure[index])) problems.Add($"seed {seed}: trail person {index} is not on its pure spot");

                // Every face's people as spawned: the towns, the town buildings and the face populations.
                world.RevealScience();
                yield return null;
                for (int f = 0; f < CubeSettings.FaceCount; f++)
                {
                    var face = (FaceId)f;
                    if (face != CubeModel.StartFace && !CubeLayout.IsLaidOut(world.Model, face)) continue;
                    interiors.RoomOf(new ScreenAddress(face, TownSquare.Cell(n)));
                }
                yield return null;
                var byFace = new Dictionary<FaceId, List<NpcTalker>>();
                foreach (NpcTalker npc in Object.FindObjectsByType<NpcTalker>(FindObjectsSortMode.None))
                {
                    if (npc.Spec == null || npc.Spec.IsLeader || npc.GetComponentInParent<LairRoomView>() != null) continue;
                    TownInteriorView room = npc.GetComponentInParent<TownInteriorView>();
                    FaceId face;
                    if (room != null) face = room.Face;
                    else if (world.TryScreenAt(npc.transform.position, out ScreenAddress at)) face = at.Face;
                    else continue;
                    if (!byFace.TryGetValue(face, out List<NpcTalker> list)) byFace[face] = list = new List<NpcTalker>();
                    list.Add(npc);
                }
                foreach (KeyValuePair<FaceId, List<NpcTalker>> face in byFace)
                {
                    List<NpcTalker> givers = face.Value.Where(t => t.Lines.Any(l => stems.Any(l.Contains))).ToList();
                    if (givers.Count != 1)
                        problems.Add($"seed {seed}: {face.Key} has {givers.Count} errand-givers: " + string.Join(", ", givers.Select(g => g.Spec.DisplayName)));
                    ErrandPlan plan = face.Key == CubeModel.StartFace ? town.Errand
                        : squares.FaceErrands.TryGetValue(face.Key, out ErrandPlan p) ? p : null;
                    if (plan == null) { problems.Add($"seed {seed}: {face.Key} has no errand"); continue; }
                    // A spare quest-giver points to the face's real errand-giver.
                    foreach (NpcTalker t in face.Value)
                        foreach (string line in t.Lines)
                            if ((line.Contains("here in town") || line.Contains("round the campfire here")) && !line.Contains(plan.Giver.Name))
                                problems.Add($"seed {seed}: {t.Spec.DisplayName} points to the wrong errand-giver: {line}");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems.Take(30)));
        }

        // ---------- Town's lost-and-found side quest ----------

        [UnityTest]
        public IEnumerator TownsLostFragment_WalkedWithRealPresses_FromTheMayor_AlongTheTrail_BackToTheMayor([Values(7, 21, 42)] int seed)
        {
            yield return Load(seed);
            FaceEnemies(false);
            TownTrailQuest trailQuest = town.Trail;
            Assert.IsNotNull(trailQuest);
            Assert.IsTrue(trailQuest.Ready, "every trail person stands in Town");
            TownTrail trail = world.TownTrail;
            Assert.AreSame(trail, trailQuest.Trail);
            IReadOnlyList<NpcSpec> specs = town.Specs;
            var said = new List<string>();
            string Name(int index) => TownPlan.Specs(seed, world.FaceSize)[index].Name;
            string Where(int index) => QuestLines.Where(ScreenOf(trailQuest.PersonAt(index).transform.position), world.FaceSize);

            // 4.18 review: Town is where everyone still mixes, so its people never turn hostile. F4 beside a trail person
            // turns their race hostile on the faces, but not them; then every race turns hostile, and the trail still runs.
            NpcTalker firstPerson = trailQuest.PersonAt(trail.Steps[0]);
            navigator.TeleportTo(ScreenOf(firstPerson.transform.position), Facing.East);
            yield return null;
            PlacePlayer((Vector2)firstPerson.transform.position + new Vector2(0.3f, 0f) * K);
            yield return null;
            yield return this.Tap(keyboard.f4Key, 2);
            Assert.IsTrue(world.Relations.IsHostile(firstPerson.Race), "F4 turns the nearest person's race hostile");
            foreach (Race race in RaceExtensions.All) world.Relations.SetHostile(race, true);
            yield return null;
            foreach (NpcTalker npc in town.Npcs.Where(n => n != null).Append(town.Leader))
            {
                Assert.IsFalse(npc.IsHostile, $"{npc.Spec.DisplayName} in Town never turns hostile");
                Assert.IsNull(npc.Relations, $"{npc.Spec.DisplayName} has no race relations");
            }

            // Before the Mayor speaks, the last person on the trail has nothing to say about it.
            int last = trail.Steps[trail.Steps.Count - 1];
            yield return TalkTo(trailQuest.PersonAt(last), said);
            Assert.IsFalse(said.Any(l => l == QuestLines.GiveLine(trailQuest.PersonAt(last).Spec.Voice)), "no fragment before the trail starts");
            Assert.IsFalse(rewards.HasFragment(CubeModel.StartFace, 1));

            // The Mayor: it went missing, and who had it last (where they really stand).
            yield return TalkTo(town.Leader, said);
            Assert.AreEqual(QuestLines.MayorLostLine(Name(trail.Steps[0]), Where(trail.Steps[0])), said[1], "the Mayor names who had it last");
            Assert.AreNotEqual(ScreenOf(town.Leader.transform.position), ScreenOf(trailQuest.PersonAt(trail.Steps[0]).transform.position),
                "the trail leads off the square");
            QuestEntry entry = world.Quests.Get(QuestLog.TownId);
            Assert.IsNotNull(entry, "the side quest joins the log");
            Assert.AreEqual(QuestText.TownAsk(Name(trail.Steps[0]), Where(trail.Steps[0])), entry.Step);

            var screens = new HashSet<ScreenAddress>();
            for (int s = 0; s < trail.Steps.Count; s++)
            {
                int index = trail.Steps[s];
                NpcTalker person = trailQuest.PersonAt(index);
                screens.Add(ScreenOf(person.transform.position));
                Voice voice = person.Spec.Voice;
                if (s == trail.FavourStep)
                {
                    yield return TalkTo(person, said);
                    Assert.AreEqual(QuestLines.FavourAskLine(voice, Name(trail.Helper), Where(trail.Helper)), said[0], $"step {s}: a favour first");
                    Assert.AreEqual(QuestText.TownFavour(Name(index), Name(trail.Helper), Where(trail.Helper)), entry.Step);
                    NpcTalker helper = trailQuest.PersonAt(trail.Helper);
                    yield return TalkTo(helper, said);
                    Assert.AreEqual(QuestLines.HelperReplyLine(helper.Spec.Voice, Name(index)), said[0], "the helper answers");
                    Assert.AreEqual(QuestText.TownFavourBack(Name(index), Where(index)), entry.Step);
                }
                yield return TalkTo(person, said);
                if (s == trail.FavourStep) Assert.AreEqual(QuestLines.FavourThanksLine(voice), said[0], "thanks for the favour");
                string onward = said[s == trail.FavourStep ? 1 : 0];
                if (s + 1 < trail.Steps.Count)
                {
                    int next = trail.Steps[s + 1];
                    Assert.AreEqual(QuestLines.PointerLine(voice, Name(next), Where(next)), onward, $"step {s} points on");
                    Assert.AreEqual(QuestText.TownAsk(Name(next), Where(next)), entry.Step);
                    Assert.IsFalse(rewards.HasFragment(CubeModel.StartFace, 1));
                }
                else
                {
                    Assert.AreEqual(QuestLines.GiveLine(voice), onward, "the last one hands it over");
                    Assert.IsTrue(rewards.HasFragment(CubeModel.StartFace, 1), "Town's fragment is the player's");
                    Assert.AreEqual(QuestText.TownReturn, entry.Step);
                }
            }
            Assert.AreEqual(trail.Steps.Count, screens.Count, "each townsperson on the trail stands on a screen of their own");
            Assert.IsTrue(screens.All(sc => sc.Face == CubeModel.StartFace), "all in Town");

            // Back to the Mayor: Town is mended, and the side quest is done.
            yield return TalkTo(town.Leader, said);
            CollectionAssert.AreEqual(Dialogue.UnsealConversation(Race.Townsfolk, 1), said);
            Assert.IsTrue(world.Repairs.IsUnsealed(CubeModel.StartFace), "Town's temple is open (4.14)");
            Assert.IsFalse(world.Repairs.IsRepaired(CubeModel.StartFace), "Town is mended only by its temple's gem");
            Assert.IsTrue(world.Quests.Has(QuestLog.TempleId(CubeModel.StartFace)), "the temple joins the log");
            Assert.IsTrue(entry.Done);
            Assert.AreEqual(QuestText.TownDone, entry.Step);
        }
    }
}
