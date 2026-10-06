using UnityEngine;

namespace Game.Cube
{
    /// <summary>Where a gate may be placed (filled by later stories; gates are data bound to items).</summary>
    public class GateSlot : ModuleSlot
    {
        public override string Label => "Gate";
        public override Color MarkerColor => new Color(1f, 0.55f, 0.1f);
    }
}
