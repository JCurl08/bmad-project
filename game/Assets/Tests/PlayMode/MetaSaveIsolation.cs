using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// Applies IsolatedMetaSave to every test in the Game.Cube.Tests namespace (a SetUpFixture's attributes reach every
    /// test below it; an assembly-level attribute does not under the Unity test runner).
    /// </summary>
    [SetUpFixture, IsolatedMetaSave]
    public class MetaSaveIsolationSetUp
    {
    }

    /// <summary>
    /// Every test in this namespace runs against its own empty meta save: the save key points at a test-only PlayerPrefs
    /// key that is deleted before and after each test, and the scene's seed is kept for the first run (deterministic).
    /// After each test the real key and the fresh-seed default are restored, so nothing stays redirected (domain reload
    /// may be off). A run that ends in one test (banking, counting the run) can then never leak into the next (hint
    /// density, upgrades), and the player's real save is never touched.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class IsolatedMetaSaveAttribute : Attribute, ITestAction
    {
        public const string TestKey = "entropy.meta.tests";

        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test)
        {
            MetaSave.PrefsKey = TestKey;
            MetaSave.Delete(TestKey);
            RunLoop.KeepSceneSeed = true;
        }

        public void AfterTest(ITest test)
        {
            MetaSave.Delete(TestKey);
            MetaSave.PrefsKey = MetaSave.DefaultPrefsKey;
            RunLoop.KeepSceneSeed = false;
        }
    }
}
