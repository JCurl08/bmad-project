using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A trigger region of a TimedDoorGate that tells the door the player made it: the threshold (a small circle in
    /// the lane just in front of the door) and the doorway (the opening and the pocket behind it, which is also a
    /// BoulderBlocker). The player reaching either while the door is open latches the door open for good (or, for a door
    /// that does not latch, counts a pass and holds it while they stay: TimedDoorGate.Reached).
    /// </summary>
    public class DoorLatch : MonoBehaviour
    {
        [SerializeField] private TimedDoorGate door;
        [SerializeField] private bool doorway = true;

        /// <summary>True for the doorway (the opening and the pocket), false for the threshold in front of the door.</summary>
        public bool IsDoorway
        {
            get => doorway;
            set => doorway = value;
        }

        public TimedDoorGate Door
        {
            get => door;
            set => door = value;
        }

        private void OnTriggerEnter2D(Collider2D other) => Reach(other);

        private void OnTriggerStay2D(Collider2D other) => Reach(other);

        private void Reach(Collider2D other)
        {
            if (door == null || other == null || other.isTrigger) return;
            if (ItemPickup.FindInventory(other) != null) door.Reached(doorway);
        }
    }
}
