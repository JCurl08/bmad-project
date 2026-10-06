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
