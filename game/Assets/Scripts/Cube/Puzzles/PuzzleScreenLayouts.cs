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

    /// <summary>
    /// A small cache of at most limit entries (the puzzle plans' layouts, the temples). 4.21 review: when full it forgets its
    /// oldest entry only, so the current run's warmed entries are never all thrown away by one more.
    /// </summary>
    public sealed class BoundedCache<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> entries = new Dictionary<TKey, TValue>();
        private readonly Queue<TKey> order = new Queue<TKey>();
        private readonly int limit;

        public BoundedCache(int limit = PuzzleScreenLayouts.CacheLimit)
        {
            this.limit = limit;
        }

        public int Count => entries.Count;

        /// <summary>Values made and kept since the start (4.21: a cache miss each; Clear does not reset it). Tests read it.</summary>
        public int Added { get; private set; }

        public bool TryGetValue(TKey key, out TValue value) => entries.TryGetValue(key, out value);

        /// <summary>Keeps a value (forgetting the oldest entry first when the cache is full).</summary>
        public void Add(TKey key, TValue value)
        {
            if (!entries.ContainsKey(key))
            {
                while (entries.Count >= Math.Max(1, limit) && order.Count > 0) entries.Remove(order.Dequeue());
                order.Enqueue(key);
            }
            entries[key] = value;
            Added++;
        }

        public void Clear()
        {
            entries.Clear();
            order.Clear();
        }
    }
}
