using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Cube.Tests
{
    /// <summary>
    /// The readability pass's feedback, on objects built in the test (far from anything a scene may hold): anything with
    /// Health flashes white for about 0.1 s when hit, every gate plays a short open effect, items and Jumbles pop up over
    /// the player for about a second, the isotope's stage shows as a label in its colour, and Escape closes a conversation
    /// at once (and does nothing when none is open).
    /// </summary>
    public class FeedbackTests : InputTestFixture
    {
        private static readonly Vector2 Origin = new Vector2(-7000f, 7000f);

        private readonly List<Object> created = new List<Object>();
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            foreach (Object o in created)
                if (o != null) Object.Destroy(o);
            created.Clear();
            base.TearDown();
        }

        private T Track<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private GameObject WithSprite(string name, Color color)
        {
            GameObject go = Track(new GameObject(name));
            go.transform.position = Origin;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = ArtCatalog.Get(ArtKey.EnemyGeneric);
            renderer.color = color;
            return go;
        }

        [UnityTest]
        public IEnumerator Damage_FlashesTheSpritesWhite_ForAboutATenthOfASecond_ThenRestoresThem()
        {
            var tint = new Color(0.3f, 0.6f, 0.9f);
            GameObject target = WithSprite("Flash Target", tint);
            var health = target.AddComponent<Health>();
            health.InvulnerableSeconds = 0f;
            SpriteRenderer renderer = target.GetComponentInChildren<SpriteRenderer>();
            Material normal = renderer.sharedMaterial;
            HitFlash flash = target.GetComponent<HitFlash>();
            Assert.IsNotNull(flash, "Every Health brings a HitFlash");
            yield return null;

            Assert.Greater(health.TakeDamage(1f, null), 0f);
            Assert.IsTrue(flash.IsFlashing);
            CollectionAssert.Contains(flash.Flashed.ToList(), renderer);
            Material white = ArtCatalog.Flash;
            Assert.IsNotNull(white, "The white flash material exists");
            Assert.AreSame(white, renderer.sharedMaterial, "Flashing draws the sprite in solid white");
            Assert.AreEqual(tint, renderer.color, "The flash never touches the tint");

            yield return new WaitForSeconds(HitFlash.DefaultSeconds + 0.1f);
            Assert.IsFalse(flash.IsFlashing);
            Assert.AreSame(normal, renderer.sharedMaterial, "The material is restored");
            Assert.AreEqual(tint, renderer.color);
        }

        [UnityTest]
        public IEnumerator ASwingRightAfterAHit_NeverCopiesTheFlashMaterial()
        {
            GameObject player = WithSprite("Swing Player", Color.white);
            var health = player.AddComponent<Health>();
            var attack = player.AddComponent<PlayerAttack>();
            SpriteRenderer body = player.GetComponentInChildren<SpriteRenderer>();
            Material normal = body.sharedMaterial;
            yield return null;

            Assert.Greater(health.TakeDamage(1f, null), 0f);
            Assert.IsTrue(player.GetComponent<HitFlash>().IsFlashing);
            Assert.AreSame(normal, HitFlash.NormalMaterial(body), "Mid-flash, the body's own material is still known");
            Assert.GreaterOrEqual(attack.TryAttack(), 0, "The swing happens");
            SpriteRenderer swing = player.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.name == "Swing");
            Assert.AreNotSame(ArtCatalog.Flash, swing.sharedMaterial, "The swing never takes the flash material");
            Assert.AreSame(normal, swing.sharedMaterial);
            yield return new WaitForSeconds(HitFlash.DefaultSeconds + 0.1f);
            Assert.AreSame(normal, body.sharedMaterial);
            Assert.AreSame(normal, swing.sharedMaterial);
        }

        [UnityTest]
        public IEnumerator AnEnemyHit_FlashesItsBodyAndItsMark()
        {
            Enemy enemy = Enemy.Spawn(null, Origin, new Rect(Origin - Vector2.one * 3f, Vector2.one * 6f));
            Track(enemy.gameObject);
            ArtCatalog.AddShape(enemy.transform, "Weakness Mark", PartShape.Circle, Enemy.MarkOffset, Vector2.one * 0.3f, Color.red, 8, null);
            yield return null;
            Assert.Greater(enemy.Health.TakeDamage(1f, null), 0f);
            HitFlash flash = enemy.GetComponent<HitFlash>();
            Assert.IsTrue(flash.IsFlashing);
            Assert.AreEqual(enemy.GetComponentsInChildren<SpriteRenderer>().Length, flash.Flashed.Count, "Body and mark flash");
            yield return new WaitForSeconds(HitFlash.DefaultSeconds + 0.1f);
            Assert.IsFalse(flash.IsFlashing);
        }

        [UnityTest]
        public IEnumerator AGateOpening_PlaysAShortOpenEffect_ThenShowsItsOpenState()
        {
            ItemDefinition item = ItemDefinition.Create("test-key", "Test Key", Theme.Biology, Color.cyan);
            GameObject block = WithSprite("Test Gate", Color.white);
            block.AddComponent<BoxCollider2D>().size = new Vector2(GateSlot.GateWidth, GateSlot.GateThickness);
            var gate = block.AddComponent<Gate>();
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            ArtCatalog.DressGate(gate, ArtKey.GateGeneric, Vector2.up);
            Assert.IsTrue(gate.ArtDressed, "The gate wears its kind's art");
            Assert.AreEqual(new Vector2(GateSlot.GateWidth, GateSlot.GateThickness), gate.Solid.size, "Dressing never moves the collider");

            GameObject toucher = Track(new GameObject("Toucher"));
            toucher.transform.position = Origin + Vector2.down;
            var touch = toucher.AddComponent<BoxCollider2D>();
            toucher.AddComponent<Inventory>().Add(item);
            yield return null;

            int before = GateOpenEffect.Played;
            Assert.IsTrue(gate.TryOpen(touch));
            Assert.AreEqual(before + 1, GateOpenEffect.Played);
            Transform effect = gate.transform.Find(GateOpenEffect.ObjectName);
            Assert.IsNotNull(effect, "The open effect plays on the gate");
            Assert.IsEmpty(effect.GetComponentsInChildren<Collider2D>(), "The effect is visual only");

            yield return new WaitForSeconds(GateOpenEffect.Seconds + 0.15f);
            Assert.IsNull(gate.transform.Find(GateOpenEffect.ObjectName), "The effect is short and removes itself");
            Assert.IsTrue(gate.IsOpen);
            Assert.IsFalse(gate.Solid.enabled);
            Assert.Less(gate.Visual.color.a, 0.5f, "Then the open state");
        }

        [UnityTest]
        public IEnumerator Pickups_PopUpOverThePlayer_ForAboutASecond()
        {
            GameObject player = Track(new GameObject("Popup Player"));
            player.transform.position = Origin;
            var inventory = player.AddComponent<Inventory>();
            var wallet = Track(new GameObject("Popup Wallet")).AddComponent<RunWallet>();
            player.AddComponent<PickupFeedback>().Wallet = wallet;
            ItemDefinition beak = BiologyBeaks.CreateItem(BeakKind.Thin);
            yield return null;

            inventory.Add(beak);
            PickupPopup item = PickupPopup.Active.LastOrDefault();
            Assert.IsNotNull(item);
            Assert.AreEqual("+" + beak.DisplayName, item.Text);
            Assert.AreEqual("+Thin Beak", item.Text);
            Assert.Greater(item.Position.y, Origin.y, "The popup floats above the player");

            Assert.IsTrue(wallet.Add(EarningSource.Hidden, "hidden:test", 5));
            PickupPopup jumbles = PickupPopup.Active.LastOrDefault();
            Assert.AreEqual("+5 Jumbles", jumbles.Text);

            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(item != null && jumbles != null, "Still up after half a second");
            yield return new WaitForSeconds(PickupPopup.Seconds);
            Assert.IsTrue(item == null && jumbles == null, "Gone after about a second");
        }

        [UnityTest]
        public IEnumerator TheIsotopeStage_ShowsAsALabelInItsColour_AndUpdatesWithTheStage()
        {
            GameObject player = Track(new GameObject("Isotope Player"));
            player.transform.position = Origin;
            var inventory = player.AddComponent<Inventory>();
            var isotope = player.AddComponent<Isotope>();
            ItemDefinition item = ChemistryIsotope.CreateItem();
            isotope.Item = item;
            isotope.HalfLife = 10f;
            yield return null;
            Assert.IsNull(isotope.StageLabel, "No label without an isotope");

            var seen = new List<IsotopeStage>();
            isotope.StageChanged += (_, stage) => seen.Add(stage);
            inventory.Add(item);
            Assert.AreEqual("GLOWING", isotope.StageLabel);
            Assert.AreEqual(ChemistryIsotope.GlowColor, isotope.StageLabelColor);

            isotope.Age = ChemistryIsotope.StageStart(IsotopeStage.Unstable, 10f) + 0.01f;
            Assert.AreEqual("UNSTABLE", isotope.StageLabel);
            Assert.AreEqual(ChemistryIsotope.UnstableColor, isotope.StageLabelColor);

            isotope.Age = ChemistryIsotope.StageStart(IsotopeStage.Lead, 10f) + 0.01f;
            Assert.AreEqual("LEAD", isotope.StageLabel);
            Assert.AreEqual(ChemistryIsotope.LeadColor, isotope.StageLabelColor);
            CollectionAssert.AreEqual(new[] { IsotopeStage.Glow, IsotopeStage.Unstable, IsotopeStage.Lead }, seen);
        }

        [UnityTest]
        public IEnumerator Escape_ClosesAConversationAtOnce_AndDoesNothingWhenClosed()
        {
            var box = Track(new GameObject("Skip Dialogue Box")).AddComponent<DialogueBox>();
            yield return null;

            // Closed: Escape does nothing.
            int closedFrame = box.ClosedFrame;
            yield return this.Tap(keyboard.escapeKey, holdFrames: 2);
            Assert.IsFalse(box.IsOpen);
            Assert.AreEqual(closedFrame, box.ClosedFrame, "Escape with no conversation open does nothing");

            Assert.IsTrue(box.Open("Talker", new[] { "flavour", "hint" }));
            yield return null;
            Assert.AreEqual("flavour", box.CurrentLine);
            yield return this.Tap(keyboard.escapeKey, holdFrames: 2);
            Assert.IsFalse(box.IsOpen, "Escape closes the conversation");
            Assert.AreEqual(0, box.Index, "Closed at once, without walking through the lines");
            Assert.IsNull(box.CurrentLine);
        }
    }
}
