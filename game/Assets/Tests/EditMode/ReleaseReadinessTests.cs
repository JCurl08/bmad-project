using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace Game.Cube.Tests
{
    /// <summary>
    /// 4.21 release readiness, the pure parts: the meta save's store (a save that never throws, the browser store chosen on
    /// WebGL and migrating from PlayerPrefs, PlayerPrefs elsewhere), the shared HUD scale (every panel fits at 960x600,
    /// 1280x800 and 1920x1200, and grows with the screen), the input actions (every control has keys and a pad button, and
    /// the prompts show their names), the generation pre-warm (a warmed reveal generates nothing), the release identity and
    /// the build's scenes and packages.
    /// </summary>
    [IsolatedMetaSave]
    public class ReleaseReadinessTests
    {
        private const string LibraryPath = "Assets/Modules/ModuleLibrary.asset";
        private const string ActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        // ---------- Saves ----------

        private static MetaSave Sample()
        {
            var save = new MetaSave { Balance = 88, RunCount = 3, Victories = 1 };
            save.SetUpgradeLevel(UpgradeShop.Id(UpgradeStat.Power), 2);
            return save;
        }

        [Test]
        public void Save_NeverThrows_WhenTheStoreFails_AndLoadStartsFresh()
        {
            var broken = new MemoryStore { Fail = true };
            bool saved = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not save"));
            Assert.DoesNotThrow(() => saved = Sample().Save(broken, "k"));
            Assert.IsFalse(saved, "a failed save says so");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not read"));
            MetaSave loaded = null;
            MetaLoadStatus status = MetaLoadStatus.Loaded;
            Assert.DoesNotThrow(() => loaded = MetaSave.Load(broken, "k", out status));
            Assert.AreEqual(MetaLoadStatus.Fresh, status);
            Assert.AreEqual(0, loaded.Balance);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not delete"));
            Assert.DoesNotThrow(() => MetaSave.Delete(broken, "k"));
        }

        [Test]
        public void Save_GoesThroughTheCurrentStore_AndFlushes()
        {
            var store = new MemoryStore();
            MetaStorage.Current = store;
            Assert.IsTrue(Sample().Save());
            Assert.IsTrue(store.Has(MetaSave.PrefsKey), "written to the current store");
            Assert.AreEqual(1, store.Flushes);
            Assert.IsFalse(PlayerPrefs.HasKey(MetaSave.PrefsKey), "not to PlayerPrefs");
            MetaSave back = MetaSave.Load(MetaSave.PrefsKey, out MetaLoadStatus status);
            Assert.AreEqual(MetaLoadStatus.Loaded, status);
            Assert.AreEqual(88, back.Balance);
            Assert.AreEqual(2, back.UpgradeLevel(UpgradeShop.Id(UpgradeStat.Power)));
        }

        [Test]
        public void OutsideWebGL_TheStoreIsPlayerPrefs_AndTheBrowserStoreIsNeverUsed()
        {
            MetaStorage.Reset();
            Assert.AreSame(PlayerPrefsStore.Instance, MetaStorage.Current, "the editor and desktop keep PlayerPrefs");
            Assert.IsFalse(BrowserStore.Available);
            Assert.Throws<System.PlatformNotSupportedException>(() => new BrowserStore().Get("x"));
            Assert.AreSame(PlayerPrefsStore.Instance, MetaStorage.Choose(false, () => new MemoryStore()));
        }

        [Test]
        public void OnWebGL_TheBrowserStoreIsChosen_AndMigratesAPlayerPrefsSaveOnFirstLoad()
        {
            // An older build's save, in PlayerPrefs.
            Sample().Save(PlayerPrefsStore.Instance, MetaSave.PrefsKey);
            var browser = new MemoryStore();
            IMetaStore chosen = MetaStorage.Choose(true, () => browser);
            Assert.IsInstanceOf<MigratingStore>(chosen);
            var migrating = (MigratingStore)chosen;
            Assert.AreSame(browser, migrating.Primary);
            Assert.AreSame(PlayerPrefsStore.Instance, migrating.Legacy);

            MetaStorage.Current = chosen;
            MetaSave first = MetaSave.Load(MetaSave.PrefsKey, out MetaLoadStatus status);
            Assert.AreEqual(MetaLoadStatus.Loaded, status, "the PlayerPrefs save is read");
            Assert.AreEqual(88, first.Balance);
            Assert.AreEqual(1, migrating.Migrated);
            Assert.IsTrue(browser.Has(MetaSave.PrefsKey), "and copied into the browser store");

            // From then on the browser store is the save: a write there wins over the old copy.
            first.Balance = 120;
            Assert.IsTrue(first.Save());
            Assert.AreEqual(120, MetaSave.Load().Balance);
            Assert.AreEqual(1, migrating.Migrated, "migrated once only");
            Assert.AreEqual(88, MetaSave.Load(PlayerPrefsStore.Instance, MetaSave.PrefsKey, out _).Balance,
                "the legacy copy is left as it was");

            // A delete clears both, so a deleted save never comes back by migration.
            MetaSave.Delete(MetaSave.PrefsKey);
            Assert.IsFalse(browser.Has(MetaSave.PrefsKey));
            Assert.IsFalse(PlayerPrefs.HasKey(MetaSave.PrefsKey));
            Assert.AreEqual(MetaLoadStatus.Fresh, StatusOf(MetaSave.PrefsKey));
        }

        [Test]
        public void EverySave_RaisesTheRevision_SoTheNewerCopyIsKnown()
        {
            var store = new MemoryStore();
            MetaSave save = Sample();
            Assert.AreEqual(0, save.Revision, "a fresh save");
            save.Save(store, "k");
            save.Save(store, "k");
            Assert.AreEqual(2, MetaSave.RevisionOf(store.Get("k")));
            Assert.AreEqual(2, MetaSave.Load(store, "k", out _).Revision);
            Assert.AreEqual(-1, MetaSave.RevisionOf(null));
            Assert.AreEqual(0, MetaSave.RevisionOf("{\"version\":1,\"balance\":3}"), "a save from before 4.21 is revision 0");
        }

        [Test]
        public void AFailedCopyIntoTheBrowserStore_StillReturnsTheLegacySave()
        {
            Sample().Save(PlayerPrefsStore.Instance, MetaSave.PrefsKey);
            var browser = new MemoryStore { FailWrites = true };
            MetaStorage.Current = new MigratingStore(browser, PlayerPrefsStore.Instance);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not copy"));
            MetaSave loaded = MetaSave.Load(MetaSave.PrefsKey, out MetaLoadStatus status);
            Assert.AreEqual(MetaLoadStatus.Loaded, status);
            Assert.AreEqual(88, loaded.Balance, "the legacy save is still read");
        }

        [Test]
        public void AWriteTheBrowserStoreRefuses_GoesToPlayerPrefs_AndTheNewerCopyWinsOnLoad()
        {
            var browser = new MemoryStore();
            var store = new MigratingStore(browser, PlayerPrefsStore.Instance);
            MetaStorage.Current = store;
            MetaSave save = Sample();
            Assert.IsTrue(save.Save(), "revision 1, in the browser store");
            browser.FailWrites = true; // full
            save.Balance = 300;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("refused"));
            Assert.IsTrue(save.Save(), "revision 2 is not lost: it goes to PlayerPrefs");
            Assert.AreEqual(1, store.FellBack);
            Assert.IsTrue(PlayerPrefs.HasKey(MetaSave.PrefsKey));
            Assert.AreEqual(1, MetaSave.RevisionOf(browser.Get(MetaSave.PrefsKey)), "the browser still holds the older copy");

            browser.FailWrites = false;
            MetaSave loaded = MetaSave.Load(MetaSave.PrefsKey, out _);
            Assert.AreEqual(300, loaded.Balance, "the newer (fallback) copy wins");
            Assert.AreEqual(2, MetaSave.RevisionOf(browser.Get(MetaSave.PrefsKey)), "and is copied back into the browser store");

            // An older legacy copy never overwrites a newer browser one.
            save.Balance = 400;
            Assert.IsTrue(save.Save()); // revision 3, browser
            Assert.AreEqual(400, MetaSave.Load(MetaSave.PrefsKey, out _).Balance);
            Assert.AreEqual(3, MetaSave.RevisionOf(browser.Get(MetaSave.PrefsKey)));

            // With both refusing, Save says so and never throws.
            browser.FailWrites = true;
            var both = new MigratingStore(browser, new MemoryStore { FailWrites = true });
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("refused"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not save"));
            Assert.IsFalse(save.Save(both, "k"));
        }

        [Test]
        public void ABrowserStoreThatCannotBeRead_NeverGetsTheLegacySaveCopiedOverIt()
        {
            Sample().Save(PlayerPrefsStore.Instance, MetaSave.PrefsKey);
            var browser = new MemoryStore();
            var newer = new MetaSave { Balance = 999 };
            newer.Save(browser, MetaSave.PrefsKey);
            newer.Save(browser, MetaSave.PrefsKey);
            newer.Save(browser, MetaSave.PrefsKey);
            browser.FailReads = true; // an error, not a missing key
            var store = new MigratingStore(browser, PlayerPrefsStore.Instance);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not read"));
            Assert.AreEqual(88, MetaSave.Load(store, MetaSave.PrefsKey, out _).Balance, "the fallback is read for now");
            Assert.AreEqual(0, store.Migrated, "but never copied over the unreadable store");
            browser.FailReads = false;
            Assert.AreEqual(999, MetaSave.Load(store, MetaSave.PrefsKey, out _).Balance, "the newer browser save is intact");
        }

        [Test]
        public void TheCache_ForgetsItsOldestEntryWhenFull_NotEverything()
        {
            var cache = new BoundedCache<int, string>(3);
            cache.Add(1, "a");
            cache.Add(2, "b");
            cache.Add(3, "c");
            cache.Add(4, "d");
            Assert.AreEqual(3, cache.Count);
            Assert.IsFalse(cache.TryGetValue(1, out _), "the oldest goes");
            Assert.IsTrue(cache.TryGetValue(2, out _) && cache.TryGetValue(3, out _) && cache.TryGetValue(4, out _));
            cache.Add(3, "c2");
            Assert.AreEqual(3, cache.Count, "replacing a key keeps the size");
            cache.Add(5, "e");
            Assert.IsFalse(cache.TryGetValue(2, out _));
            Assert.IsTrue(cache.TryGetValue(3, out string v) && v == "c2");
        }

        [Test]
        public void TheBrowserBridge_IsAWebGLOnlyPlugin_WithEveryFunctionTheStoreCalls()
        {
            string jslib = File.ReadAllText("Assets/Plugins/WebGL/MetaStorage.jslib");
            foreach (string fn in new[] { "EntropyStore_Available", "EntropyStore_Get", "EntropyStore_LastStatus", "EntropyStore_Set", "EntropyStore_Has", "EntropyStore_Delete" })
                StringAssert.Contains(fn + ":", jslib, fn);
            StringAssert.Contains("localStorage", jslib);
            string source = File.ReadAllText("Assets/Scripts/Cube/Run/MetaStorage.cs");
            StringAssert.Contains("#if UNITY_WEBGL && !UNITY_EDITOR", source, "the DllImports compile only into a WebGL player");
            var importer = AssetImporter.GetAtPath("Assets/Plugins/WebGL/MetaStorage.jslib") as PluginImporter;
            Assert.IsNotNull(importer, "the .jslib is imported as a plugin");
            Assert.IsTrue(importer.GetCompatibleWithPlatform(BuildTarget.WebGL), "for WebGL");
            Assert.IsFalse(importer.GetCompatibleWithAnyPlatform(), "only WebGL");
            Assert.IsFalse(importer.GetCompatibleWithEditor(), "never the editor");
            Assert.IsFalse(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64), "never a desktop player");
        }

        private static MetaLoadStatus StatusOf(string key)
        {
            MetaSave.Load(key, out MetaLoadStatus status);
            return status;
        }

        // ---------- The HUD scale ----------

        private static readonly Vector2[] Screens =
        {
            new Vector2(960f, 600f), new Vector2(1280f, 800f), new Vector2(1920f, 1200f),
            new Vector2(1920f, 1080f), new Vector2(800f, 600f), new Vector2(2560f, 1440f), new Vector2(1366f, 768f),
        };

        [TestCase(960f, 600f, 1f)]
        [TestCase(1280f, 800f, 4f / 3f)]
        [TestCase(1920f, 1200f, 2f)]
        [TestCase(1920f, 1080f, 1.8f)]
        [TestCase(800f, 600f, 800f / 960f)]
        public void TheScale_IsTheSmallerOfWidthOver960AndHeightOver600(float w, float h, float expected)
        {
            Assert.AreEqual(expected, HudScale.Factor(w, h), 1e-4f);
            Vector2 view = HudScale.VirtualSize(w, h);
            Assert.GreaterOrEqual(view.x, 960f - 1e-3f, "the virtual screen is never narrower than the layout's");
            Assert.GreaterOrEqual(view.y, 600f - 1e-3f, "nor shorter");
            Assert.AreEqual(w, view.x * HudScale.Factor(w, h), 1e-2f);
        }

        [Test]
        public void EveryPanel_FitsItsVirtualScreen_AtEverySize_AndGrowsWithTheScreen()
        {
            foreach (Vector2 s in Screens)
            {
                Vector2 v = HudScale.VirtualSize(s.x, s.y);
                var screen = new Rect(0f, 0f, v.x, v.y);
                CubeDebug.OverlayBars(v.x, v.y, out Rect top, out Rect bottom);
                Rect statusColumn = SideHud.Layout(v.x, v.y).Left;
                foreach (int hearts in new[] { 3, 5, 10 })
                    foreach (bool rewards in new[] { false, true })
                        foreach (bool dice in new[] { false, true })
                            foreach (Rect panel in CombatHud.StatusLayout(statusColumn, hearts, CombatHud.BareHandsLabel, rewards ? "Keys: 1 | Run boosts: +speed" : null,
                                         dice ? 6 : 0, null).All())
                            {
                                Assert.IsTrue(Inside(screen, panel), $"{s}: the status column's {panel} is on screen");
                                Assert.IsFalse(panel.Overlaps(top) || panel.Overlaps(bottom), $"{s}: the status column clears the bars");
                            }
                Rect box = DialogueBox.PanelRect(v.x, v.y);
                Assert.IsTrue(Inside(screen, box), $"{s}: the dialogue box {box}");
                Assert.IsFalse(box.Overlaps(top), $"{s}: the box clears the top bar");
                foreach (Rect r in DialogueBox.ChoiceRects(box)) Assert.IsTrue(Inside(box, r), $"{s}: an answer inside the box");

                List<Rect> entries = QuestLogView.Layout(v.x, v.y, 12);
                Assert.AreEqual(12, entries.Count, $"{s}: every quest of a run shows");
                Rect footer = QuestLogView.FooterRect(v.x, v.y);
                Assert.IsTrue(Inside(screen, footer), $"{s}: the abandon hint is on screen");
                foreach (Rect e in entries)
                {
                    Assert.IsTrue(Inside(screen, e), $"{s}: a quest entry {e}");
                    Assert.LessOrEqual(e.yMax, footer.yMin + 1e-3f, $"{s}: entries stay above the abandon hint");
                }

                CubeMap.NetFrame(v.x, v.y, CubeSettings.DefaultFaceSize, out float left, out float netTop, out float cell);
                Assert.GreaterOrEqual(left, 0f, $"{s}: the map's net is on screen");
                Assert.GreaterOrEqual(netTop, CubeMap.HeaderHeight, $"{s}: under the header");
                Assert.GreaterOrEqual(cell, 24f, $"{s}: a map screen is at least 24 units");

                // What the player sees: the panels and their text scaled by the factor, never smaller than at 960x600 on a
                // screen at least that big, and the bars still exactly on the screen's own wall bands.
                float k = HudScale.Factor(s.x, s.y);
                Rect topPixels = HudScale.ToPixels(top, s.x, s.y);
                Assert.AreEqual(CubeDebug.WallBandPixels(s.y), topPixels.height, 1e-2f, $"{s}: the top bar is the wall band");
                Assert.AreEqual(s.x, topPixels.width, 1e-2f, $"{s}: across the whole screen");
                if (s.x >= 960f && s.y >= 600f)
                {
                    Assert.GreaterOrEqual(k, 1f, $"{s}: the HUD is never shrunk on a screen at least 960x600");
                    Assert.GreaterOrEqual(HudScale.ToPixels(box, s.x, s.y).height, 134f, $"{s}: the dialogue box is at least its 960x600 size");
                }
            }
            Assert.AreEqual(2f * 20f, HudScale.ToPixels(new Rect(0f, 0f, 20f, 20f), 1920f, 1200f).height, 1e-3f,
                "at 1920x1200 the 20 px text of the dialogue box is drawn 40 px tall");
        }

        [Test]
        public void AWorldPoint_MapsToTheScaledGui()
        {
            Vector2 gui = HudScale.ToGui(new Vector3(960f, 300f, 1f), 1920f, 1200f);
            Assert.AreEqual(480f, gui.x, 1e-3f);
            Assert.AreEqual(450f, gui.y, 1e-3f, "y down, in GUI units");
            Assert.AreEqual(new Vector2(100f, 500f), HudScale.ToGui(new Vector3(100f, 100f, 0f), 960f, 600f));
        }

        // ---------- Input ----------

        private static InputActionAsset Actions()
        {
            var asset = InputActionAsset.FromJson(File.ReadAllText(ActionsPath));
            Assert.IsNotNull(asset);
            return asset;
        }

        private static bool HasBinding(InputAction action, string group) =>
            action.bindings.Any(b => !b.isComposite && b.groups != null && b.groups.Split(';').Contains(group));

        [Test]
        public void EveryControl_IsAnAction_WithKeys_AndAPadButton()
        {
            InputActionAsset asset = Actions();
            try
            {
                foreach (string path in GameActions.All())
                {
                    InputAction action = asset.FindAction(path);
                    Assert.IsNotNull(action, $"{path} is in the actions asset");
                    Assert.IsTrue(HasBinding(action, InputPrompts.KeyboardGroup), $"{path} has a key");
                    bool buyKey = path.StartsWith("Player/Buy");
                    if (!buyKey) Assert.IsTrue(HasBinding(action, InputPrompts.GamepadGroup), $"{path} has a pad binding");
                }
                // The pad reaches what the keys used to only: the log, the map, closing, Continue, and the shop's pick.
                Assert.AreEqual("D-Pad Up", InputPrompts.Names(asset.FindAction(GameActions.QuestLog), true));
                Assert.AreEqual("Select", InputPrompts.Names(asset.FindAction(GameActions.Map), true));
                Assert.AreEqual("B", InputPrompts.Names(asset.FindAction(GameActions.Close), true));
                Assert.AreEqual("Start", InputPrompts.Names(asset.FindAction(GameActions.Continue), true));
                Assert.AreEqual("A", InputPrompts.Names(asset.FindAction(GameActions.Confirm), true));
                Assert.AreEqual("D-Pad Down", InputPrompts.Names(asset.FindAction(GameActions.MenuDown), true));
                // A second close key besides Esc (Esc also leaves browser fullscreen).
                Assert.AreEqual("Esc/Backspace", InputPrompts.Names(asset.FindAction(GameActions.Close), false));
                Assert.AreEqual("M/Tab", InputPrompts.Names(asset.FindAction(GameActions.Map), false));
                Assert.AreEqual("Q", InputPrompts.Names(asset.FindAction(GameActions.QuestLog), false));
                Assert.AreEqual("Enter", InputPrompts.Names(asset.FindAction(GameActions.Continue), false));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ThePrompts_ShowTheRealKeyOrButtonNames()
        {
            InputActionAsset asset = Actions();
            try
            {
                Assert.AreEqual("[E] next   [Esc/Backspace] skip", InputPrompts.Fill(DialogueBox.NextPrompt, false, asset));
                Assert.AreEqual("[Y] next   [B] skip", InputPrompts.Fill(DialogueBox.NextPrompt, true, asset));
                Assert.AreEqual("[Stick] choose   [A] confirm", InputPrompts.Fill(DialogueBox.ChoicePromptPad, true, asset),
                    "a pad answers with A, as it takes a pick between runs");
                Assert.AreEqual("[Arrows] choose   [E] confirm", InputPrompts.Fill(DialogueBox.ChoicePrompt, false, asset));
                Assert.AreEqual("[Select] close", InputPrompts.Fill(CubeMap.CloseHint, true, asset));
                Assert.AreEqual("[D-Pad Up] quest log", InputPrompts.Fill(QuestText.MapHint, true, asset));
                Assert.AreEqual("Back to Town (new seed)  [Start]", InputPrompts.Fill(BetweenRunsScreen.ContinueLabel, true, asset));
                Assert.AreEqual("Buy [A]", InputPrompts.Fill(BetweenRunsScreen.PadBuyPicked, true, asset));
                Assert.AreEqual("Buy [3]", InputPrompts.Fill(BetweenRunsScreen.BuyLabel(2), false, asset));
                Assert.AreEqual("[X] abandon this run", InputPrompts.Fill(QuestText.AbandonHint, true, asset));
                StringAssert.Contains("[D-Pad Left] or [D-Pad Right]",
                    InputPrompts.Fill(QuestText.TourStep(TourLesson.Items, "here", true), true, asset), "the tour's item stop on a pad");
                StringAssert.Contains("press [D-Pad Up]",
                    InputPrompts.Fill(QuestText.TourStep(TourLesson.QuestLog, "here", true), true, asset), "the tour's quest-log stop on a pad");
                StringAssert.Contains("[Y]", InputPrompts.Fill(MassMitt.ReadyText, true, asset));

                // Every prompt the game shows: no action token left on either device, but the buy keys on a pad (the pad
                // buys through the pick instead).
                var prompts = new List<string>
                {
                    DialogueBox.NextPrompt, DialogueBox.ClosePrompt, DialogueBox.ChoicePrompt, DialogueBox.ChoicePromptPad,
                    CubeMap.CloseHint, QuestText.MapHint,
                    QuestText.CloseHint, QuestText.AbandonHint, BetweenRunsScreen.ContinueLabel, BetweenRunsScreen.PadBuyPicked,
                    LockText.UseKey, QuestText.Added("A title"),
                };
                prompts.AddRange(MassMitt.AllHudText);
                foreach (TourLesson lesson in TownTour.Lessons) prompts.Add(QuestText.TourStep(lesson, "here", true));
                foreach (string prompt in prompts)
                    foreach (bool pad in new[] { false, true })
                    {
                        string shown = InputPrompts.Fill(prompt, pad, asset);
                        foreach (KeyValuePair<string, string> t in InputPrompts.Tokens)
                        {
                            string token = t.Key;
                            // A token named like its own binding ("[Q]" is Q, "[Arrows]" the keyboard's move) shows as itself.
                            if (token == "[" + InputPrompts.Names(asset.FindAction(t.Value), pad) + "]") continue;
                            StringAssert.DoesNotContain(token, shown, $"{(pad ? "pad" : "keys")}: {prompt}");
                        }
                    }
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void TheShopsPick_WrapsRoundTheUpgradesAndContinue()
        {
            Assert.AreEqual(0, BetweenRunsScreen.Step(BetweenRunsScreen.NoPick, 1), "from no pick, down is the first upgrade");
            Assert.AreEqual(BetweenRunsScreen.ContinueRow, BetweenRunsScreen.Step(BetweenRunsScreen.NoPick, -1), "and up is Continue");
            Assert.AreEqual(1, BetweenRunsScreen.Step(0, 1));
            Assert.AreEqual(BetweenRunsScreen.ContinueRow, BetweenRunsScreen.Step(0, -1), "up from the first upgrade is Continue");
            Assert.AreEqual(0, BetweenRunsScreen.Step(BetweenRunsScreen.ContinueRow, 1));
            Assert.AreEqual(UpgradeShop.All.Length, BetweenRunsScreen.ContinueRow);
            Assert.IsTrue(new DialogueChoice(_ => null, startOnNo: true).StartOnNo);
            Assert.IsFalse(new DialogueChoice(_ => null).StartOnNo);
        }

        // ---------- Generation pre-warm ----------

        private static ModuleLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModuleLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"{LibraryPath} missing");
            return library;
        }

        private static int Generated() => IcePlan.Generated + WindPlan.Generated + IslandPlan.Generated + TemplePlan.Generated;

        [TestCase(7)]
        [TestCase(21)]
        [TestCase(42)]
        public void OnceWarm_TheRevealsPlansGenerateNothing_AndAreTheColdOnes(int seed)
        {
            ModuleLibrary library = Library();
            IcePlan.ClearCache();
            WindPlan.ClearCache();
            IslandPlan.ClearCache();
            TemplePlan.ClearCache();

            // Cold: what the reveal (and the unsealings) made before 4.21, timed.
            var model = new CubeModel(seed);
            DieNumbers dice = DieNumbers.Roll(model);
            var cold = Stopwatch.StartNew();
            string coldSignature = Reveal(model, CubeLayout.Generate(model, library), dice);
            cold.Stop();

            IcePlan.ClearCache();
            WindPlan.ClearCache();
            IslandPlan.ClearCache();
            TemplePlan.ClearCache();

            var warmModel = new CubeModel(seed);
            var job = new GenerationPrewarm(warmModel, library, DieNumbers.Roll(warmModel));
            int steps = 0;
            foreach (string _ in job.Steps()) steps++;
            Assert.IsTrue(job.Done);
            Assert.Greater(steps, 6, "the work is cut into many small steps");
            Assert.IsNotNull(job.LayoutFor(warmModel));
            Assert.IsNull(job.LayoutFor(new CubeModel(seed)), "only this run's model takes the warmed layout");

            int before = Generated();
            var warm = Stopwatch.StartNew();
            string warmSignature = Reveal(warmModel, job.LayoutFor(warmModel), DieNumbers.Roll(warmModel));
            warm.Stop();
            Assert.AreEqual(before, Generated(), "a warmed reveal generates no layout or temple");
            Assert.AreEqual(coldSignature, warmSignature, "and its plans are the cold reveal's, draw for draw");
            Assert.AreEqual(CubeLayout.Generate(warmModel, library).Signature(), job.Layout.Signature());
            Debug.Log($"Pre-warm seed {seed}: {steps} steps; the reveal's plans cold {cold.Elapsed.TotalMilliseconds:0.0} ms, " +
                      $"warm {warm.Elapsed.TotalMilliseconds:0.0} ms");
            Assert.Less(warm.Elapsed.TotalMilliseconds, 50.0, "the warmed reveal's plans are cheap");
        }

        /// <summary>What the reveal and the unsealings make, as one signature: the puzzle plans and every temple.</summary>
        private static string Reveal(CubeModel model, CubeLayout layout, DieNumbers dice)
        {
            IcePlan ice = IcePlan.Create(model, layout, dice);
            WindPlan wind = WindPlan.Create(model, layout, dice);
            IslandPlan islands = IslandPlan.Create(model, layout, dice);
            var parts = new List<string> { layout.Signature(), ice?.Signature(), wind?.Signature(), islands?.Signature() };
            foreach (FaceId face in TemplePlan.TempleFaces(model)) parts.Add(TemplePlan.For(model, face).Signature());
            return string.Join("|", parts);
        }

        // ---------- Release identity, scenes and packages ----------

        [Test]
        public void TheBuild_HasAName_ACompany_AndAVersion_ShownWithTheSeed()
        {
            Assert.AreEqual("Entropy Cube", PlayerSettings.productName, "the working title (assumption: renamed after the naming brainstorm)");
            Assert.AreEqual("JCurl08", PlayerSettings.companyName);
            StringAssert.IsMatch(@"^\d+\.\d+\.\d+$", PlayerSettings.bundleVersion);
            Assert.AreNotEqual("1.0", PlayerSettings.bundleVersion);
            Assert.AreEqual("Build " + PlayerSettings.bundleVersion, BetweenRunsScreen.BuildLine(Application.version));
            StringAssert.Contains("seed 12345", BetweenRunsScreen.RunLine(3, 12345, 1));
            Assert.AreEqual(ManagedStrippingLevel.High,
                PlayerSettings.GetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL), "managed stripping High");
        }

        [Test]
        public void OnlyTheGame_IsBuilt_AndTheUnusedPackagesAreGone()
        {
            string[] scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray();
            CollectionAssert.AreEqual(new[] { "Assets/Scenes/Cube.unity" }, scenes, "the build holds the game's scene only");
            Assert.IsTrue(File.Exists("Assets/Scenes/Tracer.unity"), "the Tracer scene stays for its tests");
            string manifest = File.ReadAllText("Packages/manifest.json");
            foreach (string gone in new[] { "com.unity.visualscripting", "com.unity.timeline", "com.unity.collab-proxy", "com.unity.learn.iet-framework" })
                StringAssert.DoesNotContain("\"" + gone + "\"", manifest, gone);
            foreach (string kept in new[] { "com.unity.inputsystem", "com.unity.test-framework", "com.unity.render-pipelines.universal" })
                StringAssert.Contains("\"" + kept + "\"", manifest, kept);
            StringAssert.Contains("Game.Cube", File.ReadAllText("Assets/link.xml"), "High stripping keeps the game's assembly");
        }

        private static bool Inside(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 1e-3f && inner.yMin >= outer.yMin - 1e-3f && inner.xMax <= outer.xMax + 1e-3f && inner.yMax <= outer.yMax + 1e-3f;
    }
}
