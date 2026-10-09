using Game.Tracer;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// When an Interact press may try a lock (4.9 review): the locks read Interact in LateUpdate, after every Update, and
    /// only take a press nothing else has. Never while the map is open, a conversation is open or opened or closed this
    /// frame (MassMitt's guard), an NPC with something to say is within talking range (the press is theirs), the Heavy
    /// Mitt* would grab or let go of a boulder, or the player is dead or frozen (the between-runs screen).
    /// </summary>
    public static class LockInput
    {
        private static int claimedFrame = -1;

        /// <summary>Claims this frame's press for one lock (false if another lock already took it), so one press tries one lock.</summary>
        public static bool Claim()
        {
            if (claimedFrame == Time.frameCount) return false;
            claimedFrame = Time.frameCount;
            return true;
        }

        public static bool PressIsFree(Transform player)
        {
            if (GamePause.InputBlocked || player == null) return false;
            DialogueBox box = DialogueBox.Shared;
            if (box != null && (box.IsOpen || box.OpenedFrame == Time.frameCount || box.ClosedFrame == Time.frameCount)) return false;
            Health health = player.GetComponentInParent<Health>();
            if (health != null && health.IsDead) return false;
            PlayerMover mover = player.GetComponentInParent<PlayerMover>();
            if (mover != null && !mover.enabled) return false;
            if (NpcTalker.AnyInRange(player)) return false;
            MassMitt mitt = player.GetComponentInParent<MassMitt>();
            if (mitt != null && mitt.isActiveAndEnabled && mitt.WouldInteract) return false;
            return true;
        }
    }
}
