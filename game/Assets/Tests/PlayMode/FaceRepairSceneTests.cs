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
    /// Fragments and face repair in the real Cube scene (4.8): the Mayor says Town's one fragment went missing (4.18), then takes it back and
    /// mends Town (lit statue and banners, the map's gold town, the HUD's die, +1 heart in the next fight, taken back on
    /// leaving); a science face numbered k has its k fragment chests with die-pip badges (no badge on the slots above k),
    /// its leader names the count and refuses a short hand-in, and mends the face once all k are held; a new run forgets
    /// it all. Leaders are read fresh (TownPopulation.Leader, TownSquares.LeaderOf) after every reveal.
    /// </summary>
    public class FaceRepairSceneTests : InputTestFixture
    {
        private const float K = ScreenMath.ContentScale;
        private const string SceneName = "Cube";

        private CubeWorld world;
        private CubeNavigator navigator;
        private RunRewards rewards;
        private CombatHud hud;
        private CubeMap map;
        private CoreArena arena;
        private Health health;
        private Rigidbody2D body;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = Object.FindAnyObjectByType<CubeWorld>();
            navigator = Object.FindAnyObjectByType<CubeNavigator>();
            hud = Object.FindAnyObjectByType<CombatHud>();
            map = Object.FindAnyObjectByType<CubeMap>();
            arena = Object.FindAnyObjectByType<CoreArena>();
            rewards = navigator.GetComponent<RunRewards>();
            health = navigator.GetComponent<Health>();
            body = navigator.GetComponent<Rigidbody2D>();
            Assert.AreSame(rewards, hud.Rewards);
        }

        private void NoFaceEnemies()
        {
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
            world.GetComponent<MathFace>().SpawnEnemies = false;
            world.GetComponent<EarthFace>().SpawnEnemies = false;
        }

        private void PlacePlayer(Vector2 at)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
        }

        private static NpcTalker Mayor() => Object.FindAnyObjectByType<TownPopulation>().Leader;

        private NpcTalker LeaderOf(FaceId face) => world.GetComponent<TownSquares>().LeaderOf(face);

        /// <summary>Stands the player right by a leader and talks (through NpcTalker.TryTalk); the box shows what was said.</summary>
        private IEnumerator Talk(NpcTalker leader, System.Action<bool> talked)
        {
            DialogueBox box = leader.Box;
            if (box.IsOpen) box.Close();
            yield return null; // a press never reopens the box the frame it closed
            PlacePlayer((Vector2)leader.transform.position + new Vector2(0.3f, 0f) * K);
            talked(leader.TryTalk());
        }

        [UnityTest]
        public IEnumerator TheMayor_MissesTownsFragment_ThenMendsTownOnceFound_LitInTownOnTheMapAndHud_AndAHeartForTheFight()
        {
            yield return Load();
            NoFaceEnemies();
            world.Rebuild(7);
            yield return null;
            Assert.AreEqual(1, world.Dice.Of(CubeModel.StartFace), "Town is 1");
            Assert.AreEqual(1, hud.FragmentDice.Count, "Before the reveal, only Town's die shows");
            Assert.AreEqual("Fragments: Town 0/1", hud.FragmentsText);
            Assert.AreEqual(BossBenefit.None, arena.RunBenefits);

            NpcTalker mayor = Mayor();
            Assert.IsNotNull(mayor.GetComponent<LeaderQuest>(), "The Mayor carries Town's quest");
            TownTrail trail = world.TownTrail;
            string firstName = TownPlan.Specs(world.Seed, world.FaceSize)[trail.Steps[0]].Name;
            StringAssert.Contains("gone missing", mayor.Lines[1], "the Mayor says Town's fragment went missing");
            StringAssert.Contains(firstName, mayor.Lines[1], "and names the trail's first person");
            bool talked = false;
            yield return Talk(mayor, t => talked = t);
            Assert.IsTrue(talked, "The Mayor can be talked to");
            DialogueBox box = mayor.Box;
            // 4.18: Town's fragment went missing; the Mayor names who had it last (Town's lost-and-found trail, walked in
            // QuestSceneTests). Here the fragment is simply found.
            Assert.AreEqual(Dialogue.LeaderLine(Race.Townsfolk), box.Lines[0]);
            StringAssert.Contains("gone missing", box.Lines[1]);
            Assert.IsFalse(rewards.HasFragment(CubeModel.StartFace, 1), "The Mayor no longer hands it over");
            // Walk the trail through its API (QuestSceneTests walks it with real presses): each step, the favour first.
            TownTrailQuest trailQuest = Object.FindAnyObjectByType<TownPopulation>().Trail;
            for (int s = 0; s < trail.Steps.Count; s++)
            {
                if (s == trail.FavourStep)
                {
                    trailQuest.Talk(trail.Steps[s], true);
                    trailQuest.Talk(trail.Helper, true);
                }
                Assert.IsFalse(rewards.HasFragment(CubeModel.StartFace, 1), $"not before the last step ({s})");
                trailQuest.Talk(trail.Steps[s], true);
            }
            Assert.IsTrue(rewards.HasFragment(CubeModel.StartFace, 1), "the trail's last person hands it over");
            Assert.IsTrue(world.Quests.Trail.FragmentGiven);
            Assert.IsFalse(world.Repairs.IsRepaired(CubeModel.StartFace), "Not mended until handed back");
            Assert.AreEqual("Fragments: Town 1/1", hud.FragmentsText);

            TownSquareView view = world.TownViewOf(CubeModel.StartFace);
            Assert.IsNotNull(view);
            Assert.IsFalse(view.Repaired);
            Assert.IsNull(view.RepairArt, "No repair art yet");

            yield return Talk(Mayor(), t => talked = t);
            Assert.IsTrue(talked);
            CollectionAssert.AreEqual(Dialogue.UnsealConversation(Race.Townsfolk, 1), box.Lines, "Handing it back opens Town's temple (4.14)");
            Assert.IsTrue(world.Repairs.IsUnsealed(CubeModel.StartFace));
            Assert.IsFalse(world.Repairs.IsRepaired(CubeModel.StartFace), "Not mended until the temple's puzzle is solved");
            Assert.IsTrue(view.TempleUnsealed, "The temple's seal is gone");
            Assert.AreEqual(TempleLook.Open, view.TempleLook);
            Assert.IsFalse(view.Repaired);
            Assert.AreEqual("Fragments: Town temple", hud.FragmentsText);
            Assert.AreEqual(RewardText.TempleTag, map.FragmentTag(CubeModel.StartFace));
            Assert.AreEqual(BossBenefit.None, arena.RunBenefits, "No benefit before the gem");
            yield return Talk(Mayor(), t => talked = t);
            CollectionAssert.AreEqual(Dialogue.TempleOpenConversation(Race.Townsfolk), box.Lines, "The Mayor points to the temple");
            box.Close();
            yield return null;

            // Into the temple, and its puzzle solved: the fragment mends and the gem is the player's.
            TempleRooms temples = Object.FindAnyObjectByType<TempleRooms>();
            Assert.IsTrue(temples.Enter(view.TempleDoor));
            Assert.IsTrue(navigator.InTemple);
            temples.Current.Solve();
            Assert.IsTrue(world.Repairs.IsRepaired(CubeModel.StartFace));
            Assert.IsTrue(world.Repairs.HasGem(CubeModel.StartFace));
            Assert.IsTrue(PickupPopup.Active.Any(p => p.Text == RewardText.RepairPopup(Theme.Town)));
            Assert.IsTrue(PickupPopup.Active.Any(p => p.Text == TempleText.GemPopup(Theme.Town)));
            Assert.IsTrue(temples.Leave());
            yield return null;
            Assert.IsTrue(view.Repaired, "The town shows it");
            Assert.IsTrue(view.RepairArt.activeSelf, "Halo and banners");
            Assert.AreEqual(3, view.RepairArt.GetComponentsInChildren<SpriteRenderer>().Length, "A halo and two banners");
            Assert.IsEmpty(view.RepairArt.GetComponentsInChildren<Collider2D>(), "Art only: nothing solid");
            Assert.AreEqual(TownSquareView.LitStatueTint, view.StatueArt.color, "The statue is lit");
            Assert.AreEqual(TempleLook.Mended, view.TempleLook);
            Assert.AreEqual("Fragments: Town gem", hud.FragmentsText);
            Assert.IsTrue(hud.FragmentDice[0].HasGem, "The HUD's die shows the gem");
            MapMarker townMarker = MapMarkers.FromWorld(world).Single(m => m.Kind == MapMarkerKind.Town && m.Screen.Face == CubeModel.StartFace);
            Assert.IsTrue(townMarker.Done, "The map's town is mended");
            Assert.AreEqual(RewardText.GemTag, map.FragmentTag(CubeModel.StartFace));

            yield return Talk(Mayor(), t => talked = t);
            CollectionAssert.AreEqual(Dialogue.MendedConversation(Race.Townsfolk), box.Lines, "Then a thank-you");
            box.Close();

            // The core fight: one more heart, filled, taken back on leaving.
            Assert.AreEqual(BossBenefit.ExtraHeart, arena.RunBenefits);
            Assert.IsTrue(arena.Enter(navigator));
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(BossBenefit.ExtraHeart, arena.ActiveBenefits);
            Assert.AreEqual(PlayerStats.DefaultMaxHealth + 1f, health.Max);
            Assert.AreEqual(health.Max, health.Current);
            navigator.TeleportTo(world.Model.StartScreen, Facing.East);
            yield return PhysicsSteps(2);
            Assert.IsFalse(arena.FightActive);
            Assert.AreEqual(PlayerStats.DefaultMaxHealth, health.Max, "For the fight only");

            // A new run forgets it all.
            world.Rebuild(8);
            yield return null;
            Assert.AreEqual(0, world.Repairs.Count);
            Assert.IsEmpty(rewards.Fragments);
            Assert.AreEqual(BossBenefit.None, arena.RunBenefits);
            Assert.IsFalse(world.TownViewOf(CubeModel.StartFace).Repaired);
            yield return null;
            StringAssert.Contains("gone missing", Mayor().Lines[1], "Town's fragment is lost again in the new run");
            StringAssert.Contains(TownPlan.Specs(world.Seed, world.FaceSize)[world.TownTrail.Steps[0]].Name, Mayor().Lines[1],
                "naming the new run's first trail person");
            Assert.AreEqual(0, world.Quests.Entries.Count, "and the quest log starts empty");
        }

        [UnityTest]
        public IEnumerator AScienceLeader_NamesTheCount_RefusesAShortHandIn_AndMendsTheFace_WithAllK([Values(21, 42)] int seed)
        {
            yield return Load();
            NoFaceEnemies();
            world.Rebuild(seed);
            yield return null;
            NoFaceEnemies();
            FaceId face = world.Dice.FaceNumbered(5);
            int k = world.Dice.Of(face);
            Assert.AreEqual(5, k);
            Theme theme = world.Model.ThemeOf(face);
            var townScreen = new ScreenAddress(face, TownSquare.Cell(world.FaceSize));
            navigator.TeleportTo(townScreen, Facing.East);
            Assert.IsTrue(world.ScienceRevealed);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(6, hud.FragmentDice.Count, "Every face's die once revealed");
            CollectionAssert.AreEqual(Enumerable.Range(1, 6), hud.FragmentDice.Select(d => d.Count), "In die-number order");

            // Every fragment chest is built, numbered as pips; the slots above k hold chest keys (4.9), with no die.
            foreach (RewardChest chest in world.RewardChests)
            {
                int count = world.Dice.Of(chest.Spot.Face);
                if (chest.Kind == RewardKind.FragmentSlot)
                {
                    Assert.LessOrEqual(chest.Spot.What.Ordinal, count, $"{chest.Spot}: fragments 1..k only");
                    Assert.IsNotNull(chest.Badge, $"{chest.Spot}: a die badge");
                    Assert.AreSame(TownArt.DieFace(chest.Spot.What.Ordinal), chest.Badge.sprite, "showing its number as pips");
                    Assert.AreEqual(count, chest.FragmentCount);
                }
                else Assert.IsNull(chest.transform.Find("Die Badge"), $"{chest.Spot}: no shard or die on anything else");
            }
            List<RewardChest> mine = world.RewardChests.Where(c => c.Kind == RewardKind.FragmentSlot && c.Spot.Face == face)
                .OrderBy(c => c.Spot.What.Ordinal).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(1, k), mine.Select(c => c.Spot.What.Ordinal), "The face numbered 5 has fragments 1-5");

            for (int i = 0; i < k - 1; i++) Assert.IsTrue(mine[i].Open(navigator.gameObject));
            Assert.IsTrue(PickupPopup.Active.Any(p => p.Text == RewardText.FragmentPopup(theme, k - 1, k)), "Fragment 4 of 5");
            StringAssert.Contains($"{InWorldText.Region(theme)} {k - 1}/{k}", hud.FragmentsText);
            Assert.AreEqual($"{k - 1}/{k}", map.FragmentTag(face));

            NpcTalker leader = LeaderOf(face);
            Assert.IsNotNull(leader);
            Race race = leader.Spec.Race;
            bool talked = false;
            yield return Talk(leader, t => talked = t);
            Assert.IsTrue(talked, "The leader can be talked to");
            CollectionAssert.AreEqual(Dialogue.QuestConversation(race, k, k - 1), leader.Box.Lines, "Four of five: still asking");
            StringAssert.Contains("five", leader.Box.Lines[1], "naming the count");
            Assert.IsFalse(world.Repairs.IsRepaired(face), "A short hand-in mends nothing");

            Assert.IsTrue(mine[k - 1].Open(navigator.gameObject));
            yield return Talk(LeaderOf(face), t => talked = t);
            Assert.IsTrue(talked);
            CollectionAssert.AreEqual(Dialogue.UnsealConversation(race, k), leader.Box.Lines, "All five: the temple opens (4.14)");
            Assert.IsTrue(world.Repairs.IsUnsealed(face));
            Assert.IsFalse(world.Repairs.IsRepaired(face), "not mended yet");
            Assert.AreEqual(RewardText.TempleTag, map.FragmentTag(face));
            StringAssert.Contains($"{InWorldText.Region(theme)} temple", hud.FragmentsText);
            Assert.AreEqual(BossBenefit.None, arena.RunBenefits & BossBenefits.Of(theme), "its benefit waits for the gem");
            leader.Box.Close();
            yield return null;
            TownSquareView view = world.TownViewOf(face);
            Assert.IsTrue(view.TempleUnsealed);
            TempleRooms temples = Object.FindAnyObjectByType<TempleRooms>();
            Assert.IsTrue(temples.Enter(view.TempleDoor));
            Assert.AreEqual(world.TempleOf(face).Kind, temples.Current.Puzzle.Kind);
            temples.Current.Solve();
            Assert.IsTrue(temples.Leave());
            yield return null;
            Assert.IsTrue(world.Repairs.IsRepaired(face), "the temple's gem mends the face");
            Assert.IsTrue(view.Repaired);
            Assert.IsTrue(view.RepairArt.activeSelf);
            Assert.IsEmpty(view.RepairArt.GetComponentsInChildren<Collider2D>());
            Assert.IsTrue(MapMarkers.FromWorld(world).Single(m => m.Kind == MapMarkerKind.Town && m.Screen.Face == face).Done);
            Assert.AreEqual(RewardText.GemTag, map.FragmentTag(face));
            StringAssert.Contains($"{InWorldText.Region(theme)} gem", hud.FragmentsText);
            Assert.AreEqual(BossBenefits.Of(theme), arena.RunBenefits, "Its gem's benefit waits in the core");
        }
    }
}
