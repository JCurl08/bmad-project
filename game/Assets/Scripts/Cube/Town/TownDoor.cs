using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The door of a town building (4.5), marked for story 4.6, which turns it into the way into the building's interior:
    /// where its sill is, which way it faces, and whose building it is. Only the town's one marked door carries it, with a
    /// doormat and a lantern so the player can tell it apart. Buildings are solid until then.
    /// </summary>
    public class TownDoor : MonoBehaviour
    {
        [SerializeField] private Facing facing = Facing.South;
        [SerializeField] private TownBuildingKind building;
        [SerializeField] private Theme theme;

        /// <summary>The way the door faces (out of the building, onto the lane).</summary>
        public Facing Facing => facing;

        /// <summary>The kind of building it opens.</summary>
        public TownBuildingKind Building => building;

        /// <summary>The town's race (its face theme).</summary>
        public Theme Theme => theme;

        /// <summary>The middle of the door's sill (world): this object's position.</summary>
        public Vector2 Sill => transform.position;

        public void Configure(Facing doorFacing, TownBuildingKind kind, Theme townTheme)
        {
            facing = doorFacing;
            building = kind;
            theme = townTheme;
        }
    }
}
