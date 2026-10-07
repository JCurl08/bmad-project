using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The player's side of the Earth hazards: PlayerMover's IMoveModifier. Each physics step it reports the combined
    /// modifier of the hazard zones the player stands in (HazardZone.ModifierAt), and nothing at all while the player is in
    /// the core arena or on any face but Earth (Town included), whatever stands there. It is the hazards' only way into
    /// movement, so speed upgrades and the Physics doors (which read PlayerMover.EffectiveSpeed) are untouched.
    /// </summary>
    public class HazardReceiver : MonoBehaviour, IMoveModifier
    {
        private CubeNavigator navigator;
        private bool navigatorResolved;

        /// <summary>The hazard modifier the player is under right now (None off the Earth face and in the core arena).</summary>
        public MoveModifier CurrentMoveModifier => Applies ? HazardZone.ModifierAt(transform.position) : MoveModifier.None;

        /// <summary>
        /// True when hazards may act on the player: not in the core arena, and on the Earth face (when the player has a
        /// navigator; without one, as in component tests, only the zones' areas decide).
        /// </summary>
        public bool Applies
        {
            get
            {
                if (!navigatorResolved)
                {
                    navigator = GetComponent<CubeNavigator>();
                    navigatorResolved = true;
                }
                if (navigator == null) return true;
                if (navigator.InCoreArena) return false;
                CubeWorld world = navigator.World;
                return world != null && world.Model != null && world.Model.ThemeOf(navigator.Face) == Theme.EarthAtmosphere;
            }
        }
    }
}
