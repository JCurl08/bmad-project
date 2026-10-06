using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// Something a player swing can act on besides Health: rocks and pots that break, buttons that press,
    /// flowers that pollinate, trial targets. PlayerAttack calls it once per swing for every receiver whose
    /// collider (solid or trigger) is in the swing's hitbox.
    /// </summary>
    public interface IAttackReceiver
    {
        /// <summary>Reacts to a swing carrying item (null = bare) from attacker. Returns true if it reacted.</summary>
        bool ReceiveAttack(ItemDefinition item, GameObject attacker);
    }
}
