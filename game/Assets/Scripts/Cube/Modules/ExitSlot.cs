using UnityEngine;

namespace Game.Cube
{
    /// <summary>An open side exit of a screen module. Every module has one per side.</summary>
    public class ExitSlot : ModuleSlot
    {
        [SerializeField] private Facing facing;

        public Facing Facing
        {
            get => facing;
            set => facing = value;
        }

        public override string Label => $"Exit {facing}";
        public override Color MarkerColor => new Color(0.3f, 0.9f, 1f);
    }
}
