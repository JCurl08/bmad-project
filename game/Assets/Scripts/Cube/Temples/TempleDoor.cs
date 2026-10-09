using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The door of a town's temple (4.14): where its sill is, which way it faces, whose town it is, and whether it is
    /// unsealed. TempleRooms takes a player walking into it (as TownInteriors takes the marked door) once it is unsealed;
    /// before then the building is solid and walking into it says so (TempleText.SealedPopup).
    /// </summary>
    public class TempleDoor : MonoBehaviour
    {
        [SerializeField] private Facing facing = Facing.South;
        [SerializeField] private Theme theme;
        [SerializeField] private bool unsealed;

        public Facing Facing => facing;

        /// <summary>The town's race (its face theme).</summary>
        public Theme Theme => theme;

        /// <summary>True once the face's fragments were handed in: the door leads into the temple.</summary>
        public bool Unsealed
        {
            get => unsealed;
            set => unsealed = value;
        }

        /// <summary>The middle of the door's sill (world).</summary>
        public Vector2 Sill => transform.position;

        public void Configure(Facing doorFacing, Theme townTheme)
        {
            facing = doorFacing;
            theme = townTheme;
        }
    }
}
