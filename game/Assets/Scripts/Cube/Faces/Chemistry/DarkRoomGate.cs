using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A glow gate: a curtain of darkness across the alcove opening. Touching it while holding a glowing isotope
    /// lights the room and it opens; touching it with an unstable or lead isotope, another item or nothing does
    /// nothing. A swing carrying the glowing isotope lights it too.
    /// </summary>
    public class DarkRoomGate : IsotopeGate, IAttackReceiver
    {
        public static readonly Color DarkColor = new Color(0.04f, 0.03f, 0.07f);

        public override IsotopeStage Stage => IsotopeStage.Glow;

        protected override bool OpensOnTouch => true;

        protected override Color ClosedColor => DarkColor;

        protected override bool CanOpenFor(Inventory inventory) =>
            inventory != null && Matches(inventory.GetComponent<Isotope>());

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker)
        {
            if (IsOpen || item == null || item != RequiredItem) return false;
            return Matches(Isotope.On(attacker)) && OpenNow();
        }

        public static DarkRoomGate Build(GameObject block, ItemDefinition item, Material material)
        {
            DarkRoomGate gate = Setup<DarkRoomGate>(block, $"Dark Room Gate ({item})", item, PartShape.Circle, material);
            ArtCatalog.DressGate(gate, ArtKey.GateDarkRoom);
            return gate;
        }
    }
}
