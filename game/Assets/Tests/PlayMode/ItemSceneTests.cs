using System;
using System.Collections;
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
    /// Items in the real Cube scene: the reveal instantiates exactly the seed's gates and pickups (unused
    /// gate slots stay open), and a player who picks up an item and walks to one of its gates on another
    /// face opens it, while the same gate stays shut before the item is held.
    /// </summary>
    public class ItemSceneTests : InputTestFixture
    {
        private const string SceneName = "Cube";
        private const float Timeout = 6f;

        private Keyboard keyboard;
        private CubeWorld world;
        private CubeNavigator navigator;
        private Inventory inventory;
        private Rigidbody2D body;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        /// <summary>Loads the scene, optionally starts a run on another seed, then reveals the science faces.</summary>
        private IEnumerator LoadAndReveal(Func<CubeWorld, int> chooseSeed = null)
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
            world = UnityEngine.Object.FindAnyObjectByType<CubeWorld>();
            navigator = UnityEngine.Object.FindAnyObjectByType<CubeNavigator>();
            Assert.IsNotNull(world);
            Assert.IsNotNull(navigator);
            if (chooseSeed != null)
            {
                world.Rebuild(chooseSeed(world));
                yield return null;
            }
            inventory = navigator.GetComponent<Inventory>();
            body = navigator.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(inventory, "The player has no Inventory");
            Assert.IsNotNull(world.ItemCatalog, "CubeWorld has no ItemCatalog wired in");
            Assert.IsEmpty(inventory.Items, "A run starts with no items");
            Assert.IsNull(world.ItemPlacement, "Items are placed only on the reveal");

            ScreenAddress science = world.Model.AllScreens().First(s => CubeLayout.IsLaidOut(world.Model, s.Face));
            navigator.TeleportTo(science, Facing.East);
            Assert.IsTrue(world.ScienceRevealed);
            Assert.IsNotNull(world.ItemPlacement);
            yield return null;
        }

        private KeyControl KeyFor(Facing direction)
        {
            switch (direction)
            {
                case Facing.North: return keyboard.wKey;
                case Facing.East: return keyboard.dKey;
                case Facing.South: return keyboard.sKey;
                default: return keyboard.aKey;
            }
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < end)
                yield return null;
        }

        private Gate GateAt(GatePlacement placement) =>
            world.ModuleAt(placement.Screen).Gates[placement.Slot].GetComponentInChildren<Gate>(true);

        [UnityTest]
        public IEnumerator Reveal_InstantiatesTheSeedsGatesAndPickups_UnusedSlotsStayOpen()
        {
            yield return LoadAndReveal();
            ItemPlacement expected = ItemPlacement.ForRun(new CubeModel(world.Seed), CubeLayout.Generate(new CubeModel(world.Seed), world.Library),
                world.Library, world.ItemCatalog.Themes(), world.ItemCatalog.VariantCounts());
            Assert.AreEqual(expected.Signature(), world.ItemPlacement.Signature(), "Placement differs from the seed's");
            Assert.AreEqual(world.ItemPlacement.Gates.Count, world.Gates.Count);
            Assert.AreEqual(world.ItemPlacement.OptionalGates.Count, world.OptionalGates.Count);
            Assert.AreEqual(3, world.Pickups.Count);

            foreach (ScreenModule module in world.AllModules.Where(m => m.Theme != Theme.Town))
            {
                ScreenAddress screen = world.Model.AllScreens().First(s => world.ModuleAt(s) == module);
                GateSlot[] slots = module.Gates;
                for (int i = 0; i < slots.Length; i++)
                {
                    Gate gate = slots[i].GetComponentInChildren<Gate>(true);
                    bool placed = world.ItemPlacement.TryGetGate(screen, i, out GatePlacement g);
                    bool optional = world.ItemPlacement.TryGetOptionalGate(screen, i, out OptionalGatePlacement o);
                    Assert.AreEqual(placed || optional, gate != null, $"{module.name} slot {i}: a gate exists exactly where placed");
                    if (optional)
                    {
                        Assert.AreSame(world.ItemCatalog.VariantsOf(o.Item)[o.Variant], gate.RequiredItem);
                        Assert.IsFalse(gate.IsOpen);
                        continue;
                    }
                    if (!placed) continue;
                    Assert.AreEqual(world.ItemCatalog.ItemFor(g.Item, world.Seed), gate.RequiredItem);
                    Assert.IsFalse(gate.IsOpen);
                }
            }

            foreach (PickupPlacement p in world.ItemPlacement.Pickups)
            {
                ItemPickup pickup = world.Pickups.First(x => x.Item.HomeTheme == p.Item);
                Assert.AreEqual(p.Screen.Face, world.Model.FaceOf(p.Item));
                Vector2 local = (Vector2)pickup.transform.position - world.ScreenCenter(p.Screen);
                Assert.That(Mathf.Abs(local.x), Is.LessThan(CubeWorld.ScreenSize.x / 2f), $"{p} not on its screen");
                Assert.That(Mathf.Abs(local.y), Is.LessThan(CubeWorld.ScreenSize.y / 2f), $"{p} not on its screen");
                if (p.IsGuarded)
                    Assert.Less(Vector2.Distance(world.ModuleAt(p.Screen).Gates[p.GuardSlot].PocketCentre, pickup.transform.position), 1e-3f,
                        $"{p} is not in its alcove's pocket");
            }
        }

        /// <summary>
        /// The first seed whose run has a plain (touch-opened, no variants) item with an open pickup and a gate on
        /// another face. Themes with variants (the Biology beaks) open by their own mechanics (BiologySceneTests).
        /// </summary>
        private static int SeedWithOpenPlainItem(CubeWorld w)
        {
            for (int seed = 1; seed < 500; seed++)
            {
                var model = new CubeModel(seed);
                ItemPlacement p = ItemPlacement.ForRun(model, CubeLayout.Generate(model, w.Library), w.Library,
                    w.ItemCatalog.Themes(), w.ItemCatalog.VariantCounts());
                if (p.Pickups.Any(k => IsOpenPlain(w, p, model, k))) return seed;
            }
            Assert.Fail("No seed has an open plain-item pickup with an off-home gate");
            return 0;
        }

        private static bool IsOpenPlain(CubeWorld w, ItemPlacement p, CubeModel model, PickupPlacement k) =>
            !k.IsGuarded && w.ItemCatalog.VariantsOf(k.Item).Count == 0 &&
            p.Gates.Any(g => g.Item == k.Item && g.Screen.Face != model.FaceOf(k.Item));

        [UnityTest]
        public IEnumerator PickUpItem_ThenWalkToItsGateOnAnotherFace_GateOpens()
        {
            yield return LoadAndReveal(SeedWithOpenPlainItem);
            ItemPlacement placement = world.ItemPlacement;
            PickupPlacement open = placement.Pickups.First(p => IsOpenPlain(world, placement, world.Model, p));
            ItemDefinition item = world.ItemCatalog.ItemFor(open.Item, world.Seed);
            GatePlacement offHome = placement.Gates.First(g => g.Item == open.Item && g.Screen.Face != world.Model.FaceOf(open.Item));
            Gate gate = GateAt(offHome);
            Assert.IsNotNull(gate, $"No gate object for {offHome}");
            GateSlot slot = gate.GetComponentInParent<GateSlot>();
            Facing toward = slot.Opening.Opposite();

            // Without the item the gate stays solid.
            yield return StandBefore(offHome, slot);
            Press(KeyFor(toward), queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => gate.IsOpen, 1.5f);
            Release(KeyFor(toward), queueEventOnly: true);
            yield return null;
            Assert.IsFalse(gate.IsOpen, "The gate opened without its item");
            Assert.IsTrue(gate.Solid.enabled);

            // Pick the item up: it lies in the open lane below its screen centre.
            navigator.TeleportTo(open.Screen, Facing.East);
            yield return null;
            Press(keyboard.sKey, queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => inventory.Has(item), Timeout);
            Release(keyboard.sKey, queueEventOnly: true);
            yield return null;
            Assert.IsTrue(inventory.Has(item), $"Walking onto {open} did not pick up the item");

            // Walk to its gate on the other face: it opens.
            yield return StandBefore(offHome, slot);
            Press(KeyFor(toward), queueEventOnly: true);
            yield return WaitUntilOrTimeout(() => gate.IsOpen, Timeout);
            Release(KeyFor(toward), queueEventOnly: true);
            yield return null;
            Assert.IsTrue(gate.IsOpen, $"Holding {item} did not open {offHome}");
            Assert.IsFalse(gate.Solid.enabled);
        }

        /// <summary>Teleports to the gate's screen and places the player in the lane just in front of the gate.</summary>
        private IEnumerator StandBefore(GatePlacement placement, GateSlot slot)
        {
            navigator.TeleportTo(placement.Screen, Facing.East);
            Vector2 at = (Vector2)slot.transform.position + (Vector2)slot.Opening.ToVector() * 1.1f;
            body.position = at;
            body.linearVelocity = Vector2.zero;
            navigator.transform.position = new Vector3(at.x, at.y, navigator.transform.position.z);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(placement.Screen, navigator.Current);
        }
    }
}
