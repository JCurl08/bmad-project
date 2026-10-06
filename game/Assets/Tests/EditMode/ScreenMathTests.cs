using NUnit.Framework;
using UnityEngine;

namespace Game.Tracer.Tests
{
    public class ScreenMathTests
    {
        private static readonly Vector2 Size = new Vector2(16f, 10f);

        [Test]
        public void DefaultScreenSize_Is16By10()
        {
            Assert.AreEqual(new Vector2(16f, 10f), ScreenMath.DefaultScreenSize);
        }

        [TestCase(8f, 5f, 0, 0)]     // centre of screen A
        [TestCase(24f, 5f, 1, 0)]    // centre of screen B
        [TestCase(8f, 15f, 0, 1)]    // centre of the screen above A
        public void ScreenIndex_AtCentres(float x, float y, int ix, int iy)
        {
            Assert.AreEqual(new Vector2Int(ix, iy), ScreenMath.ScreenIndex(new Vector2(x, y), Size));
        }

        [TestCase(0f, 0f, 0, 0)]        // lower-left corner belongs to screen 0
        [TestCase(15.999f, 9.999f, 0, 0)] // just inside the upper-right edge
        [TestCase(16f, 5f, 1, 0)]       // right edge is the next screen
        [TestCase(8f, 10f, 0, 1)]       // top edge is the next screen
        public void ScreenIndex_AtEdges(float x, float y, int ix, int iy)
        {
            Assert.AreEqual(new Vector2Int(ix, iy), ScreenMath.ScreenIndex(new Vector2(x, y), Size));
        }

        [TestCase(-0.001f, 5f, -1, 0)]
        [TestCase(-8f, -5f, -1, -1)]
        [TestCase(-16f, -10f, -1, -1)]  // exact negative edge stays on -1
        [TestCase(-16.001f, -10.001f, -2, -2)]
        public void ScreenIndex_NegativeCoordinates(float x, float y, int ix, int iy)
        {
            Assert.AreEqual(new Vector2Int(ix, iy), ScreenMath.ScreenIndex(new Vector2(x, y), Size));
        }

        [Test]
        public void ScreenIndex_DefaultOverload_UsesDefaultSize()
        {
            Assert.AreEqual(new Vector2Int(1, 0), ScreenMath.ScreenIndex(new Vector2(20f, 3f)));
        }

        [TestCase(0, 0, 8f, 5f)]
        [TestCase(1, 0, 24f, 5f)]
        [TestCase(-1, -1, -8f, -5f)]
        public void ScreenCenter_RoundTripsToSameIndex(int ix, int iy, float cx, float cy)
        {
            var index = new Vector2Int(ix, iy);
            Vector2 centre = ScreenMath.ScreenCenter(index, Size);
            Assert.AreEqual(new Vector2(cx, cy), centre);
            Assert.AreEqual(index, ScreenMath.ScreenIndex(centre, Size));
        }
    }
}
