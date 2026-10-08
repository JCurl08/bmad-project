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
    /// The Wildlands* (Earth and Atmosphere) in the real Cube scene. All six faces are built; the reveal builds the planned
    /// gates (every required one the rolled Stormcaller*'s, at least one on another face; the other phenomena's optional
    /// gates on Earth), the hazards (one per screen but the calm eye at the centre), the trial and the terrestrial and avian
    /// dinosaurs. Swinging the rolled wand opens every required gate, the off-face one included; the optional gates want
    /// their own wands. The hazards reach the player on Earth and never in Town or the core arena; the trial completes once.
    /// </summary>
    public class EarthSceneTests : InputTestFixture
    {
        /// <summary>Lengths and speeds below are in the first 16x10 screens' units, scaled like the game's (4.1).</summary>
        private const float K = ScreenMath.ContentScale;

        private const string SceneName = "Cube";

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private EarthFace earth;
        private Inventory inventory;
        private PlayerAttack attack;
        private PlayerMover mover;
        private Rigidbody2D body;

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
            Assert.IsNotNull(world);
            earth = world.GetComponent<EarthFace>();
            Assert.IsNotNull(earth, "The CubeWorld carries the EarthFace content hook");
            CollectionAssert.Contains(world.FaceContents, earth);
            inventory = navigator.GetComponent<Inventory>();
            attack = navigator.GetComponent<PlayerAttack>();
            mover = navigator.GetComponent<PlayerMover>();
            body = navigator.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(navigator.GetComponent<HazardReceiver>(), "The player carries the hazard receiver");
            navigator.GetComponent<PlayerStats>().MaxHealth = 999;
        }

        private IEnumerator StartRunOntoEarth(int seed)
        {
            world.Rebuild(seed);
            yield return null;
            world.GetComponent<BiologyFace>().SpawnEnemies = false;
            world.GetComponent<ChemistryFace>().SpawnEnemies = false;
            world.GetComponent<PhysicsFace>().SpawnEnemies = false;
            world.GetComponent<MathFace>().SpawnEnemies = false;
            earth.SpawnEnemies = false;
            Assert.IsFalse(world.Model.IsSealed(world.Model.FaceOf(Theme.EarthAtmosphere)), "Earth is unsealed");
            navigator.TeleportTo(new ScreenAddress(world.Model.FaceOf(Theme.EarthAtmosphere), 1, 1), Facing.East);
            Assert.IsTrue(world.ScienceRevealed);
            Assert.IsNotNull(earth.Plan);
            yield return null;
        }

        private IEnumerator PutPlayerAt(ScreenAddress screen, Vector2 at)
        {
            if (navigator.Current != screen) navigator.TeleportTo(screen, Facing.East);
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            Physics2D.SyncTransforms();
            yield return null;
        }

        /// <summary>Swings item at the gate in a slot with the real player, standing in front of it and facing it.</summary>
        private IEnumerator SwingAt(ScreenAddress screen, GateSlot slot, ItemDefinition item)
        {
            if (!inventory.Has(item)) inventory.Add(item);
            navigator.GetComponent<Equipment>().Equip(item);
            Facing toward = slot.Opening.Opposite();
            if (navigator.Current != screen) navigator.TeleportTo(screen, Facing.East);
            yield return this.TapThroughPhysics(KeyFor(keyboard, toward));
            yield return new WaitForSeconds(attack.Cooldown + 0.02f);
            yield return PutPlayerAt(screen, (Vector2)slot.transform.position - (Vector2)toward.ToVector() * 1.0f * K);
            attack.TryAttack();
            yield return null;
        }

        /// <summary>A point inside a rect (its middle first) where the player stands clear of every solid collider.</summary>
        private static Vector2 FreePointIn(Rect area)
        {
            Physics2D.SyncTransforms();
            for (float r = 0f; r < Mathf.Max(area.width, area.height); r += 0.25f * K)
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8f;
                    Vector2 p = area.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!area.Contains(p) || p.x < area.xMin + 0.5f * K || p.x > area.xMax - 0.5f * K || p.y < area.yMin + 0.5f * K || p.y > area.yMax - 0.5f * K)
                        continue;
                    if (Physics2D.OverlapCircleAll(p, 0.5f * K).All(c => c.isTrigger || c.GetComponentInParent<CubeNavigator>() != null)) return p;
                }
            return area.center;
        }

        [UnityTest]
        public IEnumerator Reveal_BuildsAllSixFaces_TheRolledGates_OptionalExtras_HazardsButTheCalmEye_TheTrial_AndDinosaurs()
        {
            yield return Load();
            yield return StartRunOntoEarth(1);
            EarthPlan plan = earth.Plan;
            CubeModel model = world.Model;
            FaceId face = model.FaceOf(Theme.EarthAtmosphere);
            for (int f = 0; f < CubeSettings.FaceCount; f++) Assert.IsFalse(model.IsSealed((FaceId)f), $"{(FaceId)f} is built");
            Assert.AreEqual(5, world.Layout.Faces.Count, "Every science face is laid out");

            ItemDefinition wand = world.ItemFor(Theme.EarthAtmosphere);
            Assert.AreEqual(StormcallerItem.KindOf(wand), plan.Rolled, "The plan's phenomenon is the rolled wand's (by id)");
            Assert.AreSame(wand, earth.Wand);

            foreach (EarthGateSpec spec in plan.Gates)
            {
                PhenomenonGate gate = world.ModuleAt(spec.Screen).Gates[spec.Slot].GetComponentInChildren<PhenomenonGate>(true);
                Assert.IsNotNull(gate, $"{spec}");
                Assert.AreEqual(spec.Kind, gate.Kind, $"{spec}");
                Assert.AreEqual(spec.Optional, gate.Optional, $"{spec}");
                CollectionAssert.Contains(spec.Optional ? world.OptionalGates : world.Gates, gate);
                if (!spec.Optional) Assert.AreSame(wand, gate.RequiredItem);
                Assert.IsFalse(gate.IsOpen);
            }
            Assert.IsTrue(plan.Gates.Any(g => !g.Optional && g.Screen.Face != face), "The rolled wand opens a gate on another face");
            Assert.IsTrue(plan.Gates.Where(g => g.Optional).All(g => g.Kind != plan.Rolled && g.Screen.Face == face));
            foreach (EarthGateSpec spec in plan.Gates.Where(g => g.Optional))
                Assert.IsFalse(world.ItemPlacement.Pickups.Any(p => p.IsGuarded && p.Screen == spec.Screen && p.GuardSlot == spec.Slot),
                    $"{spec} guards nothing required");

            // Hazards: one per screen but the calm ones; the eye at the centre has none.
            Assert.AreEqual(new ScreenAddress(face, 1, 1), plan.Eye);
            Assert.IsNull(plan.HazardOn(plan.Eye));
            Rect eyeRect = new Rect(world.ScreenCenter(plan.Eye) - CubeWorld.ScreenSize / 2f, CubeWorld.ScreenSize);
            Assert.IsFalse(earth.Zones.Any(z => z.Area.Overlaps(eyeRect)), "No hazard in the calm eye");
            Assert.IsFalse(earth.Crevices.Any(c => c.Area.Overlaps(eyeRect)), "No crevice in the calm eye");
            Assert.AreEqual(plan.Hazards.Count(h => h.Kind != HazardKind.Crevice), earth.Zones.Count);
            Assert.AreEqual(plan.Hazards.Count(h => h.Kind == HazardKind.Crevice), earth.Crevices.Count);
            foreach (HazardZone zone in earth.Zones)
            {
                Rect screenRect = new Rect(world.ScreenCenter(zone.Screen) - CubeWorld.ScreenSize / 2f, CubeWorld.ScreenSize);
                Assert.AreEqual(face, zone.Screen.Face);
                Assert.IsTrue(screenRect.Contains(zone.Area.min) && screenRect.Contains(zone.Area.max), $"{zone.name} stays on its screen");
            }

            Assert.IsNotNull(earth.Trial, "The trial is built");
            Assert.AreEqual(EarthTrial.PieceCount, earth.Trial.Pieces.Count);
            Assert.IsTrue(earth.Trial.Pieces.All(p => p.Kind == plan.Rolled));

            Assert.AreEqual(EarthPopulation.DinoCount(world.FaceSize), earth.Dinosaurs.Count(d => d != null));
            Assert.IsTrue(earth.Dinosaurs.All(d => d.Race == Race.Dinosaur));
            Assert.IsTrue(earth.Dinosaurs.Any(d => d.Spec.Variant == DinoVariant.Terrestrial), "Terrestrial dinosaurs");
            Assert.IsTrue(earth.Dinosaurs.Any(d => d.Spec.Variant == DinoVariant.Avian), "Avian dinosaurs");
            Assert.AreEqual(Dialogue.WandLine(plan.Rolled), earth.Dinosaurs[EarthPopulation.WandTeller].Lines[0]);
            Assert.AreEqual(Dialogue.HazardLine, earth.Dinosaurs[EarthPopulation.HazardTeller].Lines[0]);
            // Every face's NPCs (spawned this reveal) stand clear of every gate front and slot, Earth's off-face gates included.
            foreach (NpcTalker npc in Object.FindObjectsByType<NpcTalker>(FindObjectsSortMode.None))
            {
                var npcFace = (FaceId)Mathf.FloorToInt(npc.transform.position.x / ((world.FaceSize + 1) * CubeWorld.ScreenSize.x));
                if (npcFace == CubeModel.StartFace) continue;
                var screen = new ScreenAddress(npcFace, world.CellAt(npcFace, npc.transform.position));
                ScreenModule module = world.ModuleAt(screen);
                Vector2 feet = (Vector2)npc.transform.position - world.ScreenCenter(screen);
                foreach (ScreenMark m in FaceContent.SlotMarks(module))
                    Assert.GreaterOrEqual(Vector2.Distance(feet, m.Local), m.Radius + FaceContent.SlotKeepOut - 0.3f * K,
                        $"{npc.name} on {screen} stands on a {m.What}");
            }
            foreach (NpcTalker dino in earth.Dinosaurs)
            {
                ScreenAddress screen = new ScreenAddress(face, world.CellAt(face, dino.transform.position));
                Assert.IsFalse(plan.NearCrevice(screen, (Vector2)dino.transform.position - world.ScreenCenter(screen), 0.4f * K),
                    $"{dino.name} spawned on a crevice");
            }
        }

        [UnityTest]
        public IEnumerator TheRolledWand_OpensEveryRequiredGate_OffFaceIncluded_OptionalGatesWantTheirOwnWand()
        {
            yield return Load();
            foreach (int seed in new[] { 2, 3 })
            {
                yield return StartRunOntoEarth(seed);
                EarthPlan plan = earth.Plan;
                ItemDefinition wand = world.ItemFor(Theme.EarthAtmosphere);
                List<ItemDefinition> variants = world.ItemCatalog.VariantsOf(Theme.EarthAtmosphere);
                foreach (EarthGateSpec spec in plan.Gates)
                {
                    GateSlot slot = world.ModuleAt(spec.Screen).Gates[spec.Slot];
                    PhenomenonGate gate = slot.GetComponentInChildren<PhenomenonGate>(true);
                    if (spec.Optional)
                    {
                        yield return SwingAt(spec.Screen, slot, wand);
                        Assert.IsFalse(gate.IsOpen, $"seed {seed} {spec}: the rolled wand leaves another phenomenon's gate shut");
                        ItemDefinition own = variants.First(v => StormcallerItem.KindOf(v) == spec.Kind);
                        yield return SwingAt(spec.Screen, slot, own);
                        Assert.IsTrue(gate.IsOpen, $"seed {seed} {spec}: its own wand opens it");
                        continue;
                    }
                    yield return SwingAt(spec.Screen, slot, wand);
                    Assert.IsTrue(gate.IsOpen, $"seed {seed} {spec}: the rolled wand opens it");
                }
                inventory.Clear();
            }
        }

        [UnityTest]
        public IEnumerator Hazards_ReachThePlayerOnEarth_NeverInTownOrTheCoreArena_AndTheTrialCompletesOnce()
        {
            yield return Load();
            int seed = 4;
            yield return StartRunOntoEarth(seed);
            while (!earth.Zones.OfType<WindBand>().Any() && seed < 20) yield return StartRunOntoEarth(++seed);
            EarthPlan plan = earth.Plan;
            var receiver = navigator.GetComponent<HazardReceiver>();

            // In a wind band on Earth: pushed.
            WindBand band = earth.Zones.OfType<WindBand>().First();
            yield return PutPlayerAt(band.Screen, FreePointIn(band.Area));
            Assert.IsTrue(receiver.Applies, "On Earth the hazards apply");
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(mover.Modifier.IsNone, "In the band the wind reaches the mover");
            Assert.Greater(Vector2.Dot(body.linearVelocity, band.Direction), 0.5f * K, "Pushed along the band");
            Assert.AreEqual(TimeField.BasePlayerSpeed, mover.EffectiveSpeed, 1e-4f, "EffectiveSpeed (the Physics doors' maths) is untouched");

            // The calm eye: nothing.
            yield return PutPlayerAt(plan.Eye, world.ScreenCenter(plan.Eye));
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(mover.Modifier.IsNone, "The calm eye is calm");

            // Town: never, even standing where a zone's area would map.
            navigator.TeleportTo(new ScreenAddress(CubeModel.StartFace, 0, 0), Facing.East);
            yield return null;
            Assert.IsFalse(receiver.Applies, "No hazards in Town");
            Assert.IsTrue(receiver.CurrentMoveModifier.IsNone);

            // The core arena: never.
            var arena = Object.FindAnyObjectByType<CoreArena>();
            yield return PutPlayerAt(band.Screen, band.Area.center);
            Assert.IsTrue(arena.Enter(navigator));
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(navigator.InCoreArena);
            Assert.IsFalse(receiver.Applies, "No hazards in the core arena");
            Assert.IsTrue(mover.Modifier.IsNone);

            // The trial, struck in order with the rolled wand: pays once.
            yield return StartRunOntoEarth(seed);
            ItemDefinition wand = world.ItemFor(Theme.EarthAtmosphere);
            int paid = 0;
            earth.Trial.Completed += _ => paid++;
            foreach (EarthTrialPiece piece in earth.Trial.Pieces) piece.ReceiveAttack(wand, navigator.gameObject);
            Assert.IsTrue(earth.Trial.IsComplete);
            foreach (EarthTrialPiece piece in earth.Trial.Pieces) piece.ReceiveAttack(wand, navigator.gameObject);
            Assert.AreEqual(1, paid, "The trial fires once");
        }
    }
}
