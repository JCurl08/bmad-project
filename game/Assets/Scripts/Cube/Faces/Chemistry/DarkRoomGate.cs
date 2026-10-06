using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A Glowstone* gate: a curtain of darkness across the alcove opening. Swinging at it with the Glowstone* equipped
    /// lights the room and it opens (after HitsToOpen swings); touching it, a bare swing or any other item does nothing.
    /// </summary>
    public class DarkRoomGate : IsotopeGate, IAttackReceiver
    {
        public static readonly Color DarkColor = new Color(0.04f, 0.03f, 0.07f);

        public override IsotopeStage Stage => IsotopeStage.Glow;

        protected override Color ClosedColor => DarkColor;

        public bool ReceiveAttack(ItemDefinition item, GameObject attacker) => RegisterHit(item);

        public static DarkRoomGate Build(GameObject block, ItemDefinition item, int difficulty, bool optional, Material material,
            int faceSize = CubeSettings.DefaultFaceSize)
        {
            DarkRoomGate gate = Setup<DarkRoomGate>(block, $"Dark Room Gate ({item})", item, PartShape.Circle, difficulty,
                optional, faceSize, material);
            ArtCatalog.DressGate(gate, ArtKey.GateDarkRoom);
            return gate;
        }
    }
}
