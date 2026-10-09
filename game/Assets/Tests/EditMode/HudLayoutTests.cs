using NUnit.Framework;
using UnityEngine;

namespace Game.Cube.Tests
{
    /// <summary>
    /// 4.19 (playtest 5: "the overlay menu covers the screen"): the debug overlay is two slim bars over the screen's own wall
    /// bands, never over the floor, at 960x600 (the WebGL canvas) and other sizes, and every line still fits readably.
    /// </summary>
    public class HudLayoutTests
    {
        /// <summary>A long location line in the overlay's format (a sealed face, a long module name, two hostile races).</summary>
        private const string LongLocation =
            "Seed 2147483647  N=3  unrevealed  |  Face Front  Ember Vale* (sealed)  Cell (2,2)  entered facing North  |  " +
            "EarthAtmosphere Town Square  |  Hostile: Finch, Mushroom";

        [TestCase(960f, 600f)]
        [TestCase(1280f, 800f)]
        [TestCase(1920f, 1080f)]
        [TestCase(800f, 600f)]
        public void TheOverlayBars_LieOverTheWallBands_NeverOverTheFloor(float w, float h)
        {
            CubeDebug.OverlayBars(w, h, out Rect top, out Rect bottom);
            float band = CubeDebug.WallBandPixels(h);
            // The floor as drawn: the screen inside its north and south wall bands (the camera frames the screen's height).
            Rect floor = Rect.MinMaxRect(0f, band, w, h - band);
            Assert.LessOrEqual(top.yMax, floor.yMin + 1e-3f, "the top bar stays on the north wall");
            Assert.GreaterOrEqual(bottom.yMin, floor.yMax - 1e-3f, "the bottom bar stays on the south wall");
            Assert.AreEqual(0f, top.yMin);
            Assert.AreEqual(h, bottom.yMax, 1e-3f);
            Assert.AreEqual(w, top.width);
            Assert.AreEqual(w, bottom.width);
            Assert.GreaterOrEqual(top.height, CubeDebug.OverlayFontSize(h), "a line's height fits the band");
        }

        [Test]
        public void At960x600_TheBarsAre20PxTall_AndEveryLineFitsAtAReadableSize()
        {
            CubeDebug.OverlayBars(960f, 600f, out Rect top, out Rect bottom);
            Assert.AreEqual(20f, top.height, 1e-3f, "the wall band at 960x600");
            Assert.AreEqual(580f, bottom.y, 1e-3f);
            int font = CubeDebug.OverlayFontSize(600f);
            Assert.GreaterOrEqual(font, 11, "readable: at least 11 px");
            float inner = 960f - 16f;
            // A safe width (0.5 em a character; Arial averages about 0.45 em on these lines).
            Assert.LessOrEqual(CubeDebug.EstimatedWidth(CubeDebug.KeysLine, 11), inner, "every key fits on the bottom bar at 11 px");
            Assert.LessOrEqual(CubeDebug.EstimatedWidth(LongLocation, 10), inner, "a long location line fits the top bar at 10 px");
            // The old panel (8, 8, 820 x 170) covered the top-left of the floor; nothing of the overlay reaches below 20 px now.
            Assert.IsFalse(top.Overlaps(new Rect(0f, 20f, 960f, 560f)));
            Assert.IsFalse(bottom.Overlaps(new Rect(0f, 20f, 960f, 560f)));
        }

        [Test]
        public void At960x600_TheBarsKeepClearOfTheCombatPanel_AndTheKeysBarStepsAsideForADialogue()
        {
            CubeDebug.OverlayBars(960f, 600f, out Rect top, out Rect bottom);
            foreach (int hearts in new[] { 3, 5, 10 })
                foreach (bool rewards in new[] { false, true })
                    foreach (bool dice in new[] { false, true })
                    {
                        Rect panel = CombatHud.PanelRect(960f, 600f, hearts, rewards, dice);
                        Assert.IsFalse(panel.Overlaps(top), $"the combat panel ({panel}) clears the top bar");
                        Assert.IsFalse(panel.Overlaps(bottom), $"the combat panel ({panel}) clears the bottom bar");
                    }
            Rect box = DialogueBox.PanelRect(960f, 600f);
            Assert.IsFalse(box.Overlaps(top), "the dialogue box clears the top bar");
            Assert.IsTrue(box.Overlaps(bottom), "the dialogue box reaches into the bottom wall band...");
            Assert.IsFalse(CubeDebug.BottomBarShown(true), "...so the keys bar is hidden while a dialogue is open");
            Assert.IsTrue(CubeDebug.BottomBarShown(false), "and shown otherwise");
        }

        [Test]
        public void TheKeysLine_MeasuredInTheBuiltInFont_FitsAt960x600_At11PxOrMore()
        {
            float w = CubeDebug.MeasuredWidth(CubeDebug.KeysLine, 12);
            Assert.Greater(w, 100f, "the built-in font measures text");
            int size = CubeDebug.FittedFontSize(CubeDebug.KeysLine, 960f, 600f);
            Assert.GreaterOrEqual(size, 11);
            Assert.LessOrEqual(CubeDebug.MeasuredWidth(CubeDebug.KeysLine, size), 960f - 16f);
        }

        [Test]
        public void TheKeysLine_StillNamesEveryDebugKey()
        {
            foreach (string key in new[] { "F1 ", "F2 ", "F3 ", "F4 ", "F5 ", "F6 ", "F7 ", "F8 ", "F9 ", "F10 ", "F11 ", "M map", "Q quests", "Shift" })
                StringAssert.Contains(key, CubeDebug.KeysLine);
            foreach (string what in new[] { "leaders", "solve", "fragments", "arches", "reroll", "hostile", "enemy", "slots", "rest", "run" })
                StringAssert.Contains(what, CubeDebug.KeysLine);
        }
    }
}
