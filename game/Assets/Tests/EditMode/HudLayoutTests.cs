using System.Collections.Generic;
using System.Linq;
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
            // The floor as drawn (4.25: the play area between the side columns, SideHud): inside its north and south wall bands.
            Rect play = SideHud.Layout(w, h).Play;
            float band = CubeDebug.WallBandPixels(play.height);
            Rect floor = Rect.MinMaxRect(play.xMin, play.yMin + band, play.xMax, play.yMax - band);
            Assert.LessOrEqual(top.yMax, floor.yMin + 1e-3f, "the top bar stays above the floor (on the north wall or the band over it)");
            Assert.GreaterOrEqual(bottom.yMin, floor.yMax - 1e-3f, "the bottom bar stays below the floor");
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
            Rect left = SideHud.Layout(960f, 600f).Left;
            foreach (int hearts in new[] { 3, 5, 10 })
                foreach (bool rewards in new[] { false, true })
                    foreach (bool dice in new[] { false, true })
                        foreach (Rect part in Status(left, hearts, rewards, dice).All())
                        {
                            Assert.IsFalse(part.Overlaps(top), $"the status column ({part}) clears the top bar");
                            Assert.IsFalse(part.Overlaps(bottom), $"the status column ({part}) clears the bottom bar");
                        }
            Rect box = DialogueBox.PanelRect(960f, 600f);
            Assert.IsFalse(box.Overlaps(top), "the dialogue box clears the top bar");
            Assert.IsTrue(box.Overlaps(bottom), "the dialogue box reaches into the bottom wall band...");
            Assert.IsFalse(CubeDebug.BottomBarShown(true), "...so the keys bar is hidden while a dialogue is open");
            Assert.IsTrue(CubeDebug.BottomBarShown(false), "and shown otherwise");
        }

        /// <summary>A long equipped line, the longest rewards line and two item lines (the stone's and the mitt's).</summary>
        private static CombatHud.StatusRects Status(Rect column, int hearts, bool rewards, bool dice) =>
            CombatHud.StatusLayout(column, hearts, "Equipped: " + CombatHud.BareHandsLabel,
                rewards ? "Keys: 3 | Chest keys: 2 | Run boosts: +2 hearts, +1 speed, +1 power" : null, dice ? 6 : 0,
                new[] { "Unstable Rock*: swing it at cracked walls to blast them open", MassMitt.DraggingText });

        // 4.25 (playtest 7): the HUD lives in the side columns; nothing overlays the play area at 960x600 or any wider window.
        [TestCase(960f, 600f)]
        [TestCase(1280f, 720f)]
        [TestCase(1920f, 1080f)]
        [TestCase(1024f, 768f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1280f, 800f)]
        public void TheHud_LivesInTheSideColumns_NeverOverThePlayArea(float pixelsW, float pixelsH)
        {
            Vector2 v = HudScale.VirtualSize(pixelsW, pixelsH);
            SideHud.Frame f = SideHud.Layout(v.x, v.y);
            var screen = new Rect(0f, 0f, v.x, v.y);
            Rect play = f.Play;
            Assert.AreEqual(1.6f, play.width / play.height, 1e-3f, "the play area keeps the screen's aspect");
            Assert.IsTrue(Inside(screen, play), "on screen");
            Assert.AreEqual(play.xMin, f.Left.xMax, 1e-3f, "the left column meets it");
            Assert.AreEqual(play.xMax, f.Right.xMin, 1e-3f, "and so does the right");
            Assert.GreaterOrEqual(f.Left.width, SideHud.PanelWidth - 1e-3f, "room for the panels each side");
            Assert.GreaterOrEqual(f.Right.width, SideHud.PanelWidth - 1e-3f);
            Assert.AreEqual(f.Left.width, f.Right.width, 1e-3f, "centred");
            Assert.AreEqual(v.y - play.yMax, play.yMin, 1e-3f, "centred up and down");
            if (v.x / v.y >= 1.6f + 2f * SideHud.PanelWidth / v.y) Assert.AreEqual(v.y, play.height, 1e-3f, "a wide window keeps the whole height");
            Assert.GreaterOrEqual(play.width, 0.7f * Mathf.Min(v.x, v.y * 1.6f), "the play area keeps most of its width");

            // Every HUD part: the status column (hearts 3-10, rewards, dice, item lines), the quest summary and keys, the core
            // meter with its help and exit hint, all inside their columns, never over the play area.
            foreach (int hearts in new[] { 3, 5, 10 })
                foreach (bool rewards in new[] { false, true })
                    foreach (bool dice in new[] { false, true })
                    {
                        CombatHud.StatusRects s = Status(f.Left, hearts, rewards, dice);
                        foreach (Rect part in s.All())
                        {
                            Assert.IsTrue(Inside(f.Left, part), $"{pixelsW}x{pixelsH}: a status part {part} in the left column {f.Left}");
                            Assert.IsFalse(part.Overlaps(play), $"{pixelsW}x{pixelsH}: a status part over the play area");
                        }
                        Assert.LessOrEqual(s.Bottom, SideHud.Inner(f.Left).yMax + 1e-3f, $"{pixelsW}x{pixelsH}: the status column fits its height");
                    }
            List<Rect> info = SideHudView.InfoLayout(f.Right, "Mayor Muddleton's tour", "Follow the glowing markers: the first one's on Town's north-east screen.",
                new[] { "[M] map", "[Q] quest log" });
            CoreHud.MeterRects meter = CoreHud.MeterLayout(f.Right, CoreHud.MeterLabel + " (Mixing)", "Help from your gems: +1 heart, wider gaps, slower shots",
                CoreHud.ExitHint);
            foreach (Rect part in info.Concat(new[] { meter.Title, meter.Bar, meter.Help, meter.Hint }))
            {
                Assert.IsTrue(Inside(f.Right, part), $"{pixelsW}x{pixelsH}: {part} in the right column {f.Right}");
                Assert.IsFalse(part.Overlaps(play), $"{pixelsW}x{pixelsH}: {part} over the play area");
            }
            Assert.Less(info[info.Count - 1].yMax, meter.Title.yMin, $"{pixelsW}x{pixelsH}: the quest summary and the meter never meet");

            // The camera draws exactly the play area.
            Rect cam = SideHud.CameraRect(pixelsW, pixelsH);
            Assert.AreEqual(play.x / v.x, cam.x, 1e-4f);
            Assert.AreEqual(play.width / v.x, cam.width, 1e-4f);
            Assert.AreEqual(1f - play.yMax / v.y, cam.y, 1e-4f);
            Assert.AreEqual(play.height / v.y, cam.height, 1e-4f);
        }

        [Test]
        public void At960x600_EveryGemsHelpAndTheLongestQuestStep_NeverMeet_TheStepGivesUpLines()
        {
            // 4.25 review: in the core with all six gems (the longest help line) and the exit hint, the quest summary stays above
            // the meter: the step is cut to whole lines that fit.
            SideHud.Frame f = SideHud.Layout(960f, 600f);
            BossBenefit all = BossBenefit.None;
            foreach (BossBenefit b in BossBenefits.All) all |= b;
            string help = BossBenefits.HudLine(all);
            Assert.IsNotNull(help);
            CoreHud.MeterRects meter = CoreHud.MeterLayout(f.Right, CoreHud.MeterLabel + " (Mixing)", help, CoreHud.ExitHint);
            string longest = string.Join(" ", Enumerable.Repeat("Bring every fragment back to the leader by the statue.", 4));
            Assert.GreaterOrEqual(longest.Length, Dialogue.MaxLineLength, "a step longer than any line the game says");
            List<Rect> info = SideHudView.InfoLayout(f.Right, "Duchess Flarella's fragments of the Ember Vale*", longest,
                new[] { "[M/Tab] map", "[Q] quest log" }, meter.Title.yMin - 8f);
            foreach (Rect r in info)
                Assert.LessOrEqual(r.yMax, meter.Title.yMin, $"{r} stays above the meter at {meter.Title.yMin}");
            Assert.GreaterOrEqual(info[2].height, SideHud.LineHeight(SideHudView.StepSize), "at least one line of the step shows");
            Assert.Less(info[2].height, SideHud.TextHeight(longest, SideHudView.StepSize, info[2].width), "the step gave up lines");
            foreach (Rect r in new[] { meter.Title, meter.Bar, meter.Help, meter.Hint })
                Assert.IsTrue(Inside(f.Right, r) && !r.Overlaps(f.Play), $"the meter's {r} in its column");
        }

        [Test]
        public void TheWrap_KeepsWordsWhole_AndEveryLineInsideItsWidth()
        {
            List<string> lines = SideHud.Wrap("Equipped: Bare hands (a very sincere bonk)", 13, 109f);
            Assert.Greater(lines.Count, 1);
            foreach (string line in lines) Assert.LessOrEqual(line.Length * SideHud.CharWidth(13), 109f + 1e-3f, line);
            Assert.AreEqual("Equipped: Bare hands (a very sincere bonk)", string.Join(" ", lines));
            Assert.AreEqual(2, SideHud.Wrap("Keys: 1\nRun boosts: +speed", 12, 200f).Count, "a line break breaks");
        }

        private static bool Inside(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 1e-3f && inner.yMin >= outer.yMin - 1e-3f && inner.xMax <= outer.xMax + 1e-3f && inner.yMax <= outer.yMax + 1e-3f;

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
