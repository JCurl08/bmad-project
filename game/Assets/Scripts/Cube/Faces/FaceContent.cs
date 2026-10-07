using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The parts of a science face's population code that every face shares: the seeded screen order, picking a spot
    /// round the face, an NPC's or enemy's wander bounds and spawn point, finding the player, and tearing the
    /// population down on a rebuild. Callers draw from their own population stream in the same order as before.
    /// </summary>
    public static class FaceContent
    {
        /// <summary>NPCs and enemies spawn at least this far beyond a gate front or a hidden-item or core-entrance slot.</summary>
        public const float SlotKeepOut = 1.2f;

        /// <summary>Radii of the slot marks ClearOfSlots keeps spawns off (a gate's front, a hidden item, a core entrance).</summary>
        public const float HiddenSlotRadius = 0.45f, CoreSlotRadius = 0.6f;

        /// <summary>
        /// The spots on a module's screen (local) that spawns keep clear of: the front of every gate slot (whatever gate
        /// stands there, from any face: the home face's, another face's off-home gate, an optional one, or none yet), and
        /// every hidden-item and core-entrance slot.
        /// </summary>
        public static List<ScreenMark> SlotMarks(ScreenModule module)
        {
            var marks = new List<ScreenMark>();
            if (module == null) return marks;
            Vector2 origin = module.transform.position;
            foreach (GateSlot slot in module.Gates)
                marks.Add(new ScreenMark("gate front", EarthPlan.FrontLocal((Vector2)slot.transform.position - origin, slot.PocketOffset),
                    EarthPlan.FrontRadius));
            foreach (HiddenItemSlot h in module.HiddenItems)
                marks.Add(new ScreenMark("hidden-item slot", (Vector2)h.transform.position - origin, HiddenSlotRadius));
            foreach (CoreEntranceSlot c in module.CoreEntrances)
                marks.Add(new ScreenMark("core-entrance slot", (Vector2)c.transform.position - origin, CoreSlotRadius));
            return marks;
        }

        /// <summary>True when an NPC or enemy standing with its feet here (local) keeps SlotKeepOut clear of every slot mark.</summary>
        public static bool IsClearOfSlots(IReadOnlyList<ScreenMark> marks, Vector2 feet)
        {
            Vector2 body = feet + new Vector2(0f, NpcFactory.MaxBodyHeight / 2f);
            foreach (ScreenMark m in marks)
                if (Vector2.Distance(feet, m.Local) < m.Radius + SlotKeepOut || Vector2.Distance(body, m.Local) < m.Radius + SlotKeepOut)
                    return false;
            return true;
        }

        /// <summary>
        /// A FaceSpots filter for a world's screens (the faces without one of their own: Biology, Chemistry, Physics): spots
        /// clear of every gate front (Earth's off-face gates and every other face's included) and slot (SlotMarks).
        /// </summary>
        public static System.Func<ScreenAddress, Vector2, bool> ClearOfSlots(CubeWorld world)
        {
            var cache = new Dictionary<ScreenAddress, List<ScreenMark>>();
            return (screen, feet) =>
            {
                if (!cache.TryGetValue(screen, out List<ScreenMark> marks)) cache[screen] = marks = SlotMarks(world.ModuleAt(screen));
                return IsClearOfSlots(marks, feet);
            };
        }

        /// <summary>The face's screens row by row, then shuffled on rng (one draw sequence per call).</summary>
        public static List<ScreenAddress> ShuffledScreens(FaceId face, int faceSize, SeededRng rng)
        {
            var screens = new List<ScreenAddress>();
            for (int y = 0; y < faceSize; y++)
                for (int x = 0; x < faceSize; x++)
                    screens.Add(new ScreenAddress(face, x, y));
            rng.Shuffle(screens);
            return screens;
        }

        /// <summary>A spot on screens[start], else on the next screens round the face.</summary>
        public static bool TryPickAnywhere(FaceSpots spots, List<ScreenAddress> screens, int start,
            out ScreenAddress screen, out Vector2 feet)
        {
            for (int k = 0; k < screens.Count; k++)
            {
                screen = screens[(start + k) % screens.Count];
                if (spots.TryPick(screen, out feet)) return true;
            }
            screen = default;
            feet = default;
            return false;
        }

        /// <summary>World wander bounds for something standing at feet (local to a screen centre): its screen quadrant.</summary>
        public static Rect WanderBounds(Vector2 centre, Vector2 feet)
        {
            Rect quadrant = TownPlan.QuadrantBounds(feet);
            return new Rect(centre + quadrant.position, quadrant.size);
        }

        /// <summary>Where an enemy spawns for a spot: its body centre sits one body radius above the spot's feet.</summary>
        public static Vector2 EnemyPosition(Vector2 centre, Vector2 feet) => centre + feet + new Vector2(0f, Enemy.BodyRadius);

        /// <summary>The player: the given one, else the scene's CubeNavigator (remembered in player).</summary>
        public static Transform ResolvePlayer(ref Transform player)
        {
            if (player != null) return player;
            var navigator = Object.FindAnyObjectByType<CubeNavigator>();
            if (navigator != null) player = navigator.transform;
            return player;
        }

        /// <summary>Deactivates and destroys every live object of the list, then empties it.</summary>
        public static void DestroyAll<T>(List<T> items) where T : Component
        {
            foreach (T item in items)
            {
                if (item == null) continue;
                item.gameObject.SetActive(false);
                Object.Destroy(item.gameObject);
            }
            items.Clear();
        }
    }
}
