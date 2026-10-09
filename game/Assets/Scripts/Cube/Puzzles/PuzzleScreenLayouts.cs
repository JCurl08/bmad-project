using System;
using System.Collections.Generic;

namespace Game.Cube
{
    /// <summary>
    /// The plumbing the whole-screen puzzle plans share (4.15: IcePlan, WindPlan, IslandPlan): which doorways a screen has, the
    /// seed of a screen's own stream, and the bounded layout cache (a run generates each layout once).
    /// </summary>
    public static class PuzzleScreenLayouts
    {
        /// <summary>The most layouts a plan's cache holds before it starts again.</summary>
        public const int CacheLimit = 256;

        /// <summary>A screen's doorways as a bit mask (bit f set when the player can step out through side f).</summary>
        public static int DoorMask(CubeModel model, ScreenAddress screen)
        {
            int doors = 0;
            for (int f = 0; f < 4; f++)
                if (model.TryStep(screen, (Facing)f, out _, out _)) doors |= 1 << f;
            return doors;
        }

        /// <summary>The doorway test of a mask (DoorMask).</summary>
        public static Func<Facing, bool> HasDoor(int doors) => side => (doors & (1 << (int)side)) != 0;

        /// <summary>The seed of a screen's own stream: the run seed above, the screen below.</summary>
        public static ulong ScreenSeed(int seed, ScreenAddress s) =>
            ((ulong)(uint)seed << 32) | (uint)((int)s.Face * 64 + s.Cell.x * 8 + s.Cell.y);
    }

    /// <summary>A small cache that empties itself once it holds limit entries (the puzzle plans' layouts).</summary>
    public sealed class BoundedCache<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> entries = new Dictionary<TKey, TValue>();
        private readonly int limit;

        public BoundedCache(int limit = PuzzleScreenLayouts.CacheLimit)
        {
            this.limit = limit;
        }

        public int Count => entries.Count;

        public bool TryGetValue(TKey key, out TValue value) => entries.TryGetValue(key, out value);

        /// <summary>Keeps a value (emptying the cache first when it is full).</summary>
        public void Add(TKey key, TValue value)
        {
            if (entries.Count >= limit) entries.Clear();
            entries[key] = value;
        }

        public void Clear() => entries.Clear();
    }
}
