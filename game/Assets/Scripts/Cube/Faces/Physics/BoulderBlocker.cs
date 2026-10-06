using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Marks a trigger collider that a dragged boulder may not enter (a timed door's doorway and the pocket behind
    /// it), so a boulder can never be lost behind a door that closes. The player walks through it freely.
    /// </summary>
    public class BoulderBlocker : MonoBehaviour
    {
    }
}
