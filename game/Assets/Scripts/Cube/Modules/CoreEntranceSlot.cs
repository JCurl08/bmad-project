using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A possible entrance to the cube's core. Each built science face activates exactly one of these
    /// (chosen by CubeLayout); all others are deactivated.
    /// </summary>
    public class CoreEntranceSlot : ModuleSlot
    {
        public override string Label => "Core entrance";
        public override Color MarkerColor => new Color(1f, 0.25f, 0.6f);
    }
}
