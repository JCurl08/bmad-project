using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tracer.Tests
{
    /// <summary>I/O matrix rows "First input" and "Save round trip".</summary>
    public class ProbeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private GameObject probe;
        private bool hadSavedCounter;
        private int savedCounter;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();

            // Preserve the developer's real saved counter; restored in TearDown.
            hadSavedCounter = PlayerPrefs.HasKey(SaveProbe.CounterKey);
            savedCounter = PlayerPrefs.GetInt(SaveProbe.CounterKey, 0);
            PlayerPrefs.DeleteKey(SaveProbe.CounterKey);
        }

        public override void TearDown()
        {
            if (probe != null) Object.Destroy(probe);
            if (hadSavedCounter) PlayerPrefs.SetInt(SaveProbe.CounterKey, savedCounter);
            else PlayerPrefs.DeleteKey(SaveProbe.CounterKey);
            PlayerPrefs.Save();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator FirstInput_SilentUntilKeyPress_ThenPlaysTone()
        {
            probe = new GameObject("AudioProbe", typeof(AudioSource), typeof(AudioUnlock));
            var unlock = probe.GetComponent<AudioUnlock>();
            var source = probe.GetComponent<AudioSource>();

            for (int i = 0; i < 5; i++) yield return null;

            Assert.IsFalse(unlock.Unlocked, "Audio unlocked without input");
            Assert.IsFalse(source.isPlaying, "Tone played before the first input");
            Assert.IsNotNull(source.clip, "Tone clip was not generated");

            Press(keyboard.enterKey, queueEventOnly: true);
            yield return null;
            yield return null;

            Assert.IsTrue(unlock.Unlocked, "First key press did not unlock audio");
            Assert.IsTrue(source.isPlaying, "Tone did not start after the first input");
            Release(keyboard.enterKey, queueEventOnly: true);
        }

        [UnityTest]
        public IEnumerator SaveRoundTrip_MissingKeyReadsZero()
        {
            probe = new GameObject("SaveProbe", typeof(SaveProbe));
            yield return null;
            yield return null;

            Assert.AreEqual(0, probe.GetComponent<SaveProbe>().Counter);
        }

        [UnityTest]
        public IEnumerator SaveRoundTrip_ReadsSavedValueOnStart()
        {
            PlayerPrefs.SetInt(SaveProbe.CounterKey, 5);
            PlayerPrefs.Save();

            probe = new GameObject("SaveProbe", typeof(SaveProbe));
            yield return null;
            yield return null;

            Assert.AreEqual(5, probe.GetComponent<SaveProbe>().Counter);
        }

        [UnityTest]
        public IEnumerator SaveRoundTrip_SpaceIncrementsAndSaves()
        {
            PlayerPrefs.SetInt(SaveProbe.CounterKey, 5);
            PlayerPrefs.Save();

            probe = new GameObject("SaveProbe", typeof(SaveProbe));
            var save = probe.GetComponent<SaveProbe>();
            yield return null;
            yield return null;
            Assert.AreEqual(5, save.Counter);

            Press(keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(keyboard.spaceKey, queueEventOnly: true);
            yield return null;

            Assert.AreEqual(6, save.Counter, "Space did not increment the counter");
            Assert.AreEqual(6, PlayerPrefs.GetInt(SaveProbe.CounterKey, 0), "Counter was not saved to PlayerPrefs");
        }
    }
}
