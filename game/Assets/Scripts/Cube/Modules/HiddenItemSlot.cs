using UnityEngine;

namespace Game.Cube
{
    /// <summary>Where a hidden item may be placed (filled by later stories).</summary>
    public class HiddenItemSlot : ModuleSlot
    {
        public override string Label => "Hidden item";
        public override Color MarkerColor => new Color(1f, 0.95f, 0.2f);
    }
}
