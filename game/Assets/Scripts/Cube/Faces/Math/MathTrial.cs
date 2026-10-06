using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The Math trial: a bigger island-and-bridge layout (MathPlan.TrialBridges bridges, more than any door's) on one
    /// seeded Math screen. Walking it crossing every bridge exactly once calls TrialRoom.Complete, so Completed fires once
    /// with the currency amount; later walks do nothing.
    /// </summary>
    public class MathTrial : TrialRoom
    {
        public static readonly Color FlagColor = new Color(1f, 0.85f, 0.2f);

        public BridgePuzzle Puzzle { get; private set; }

        protected virtual void Awake()
        {
            CompleteOnPlayerTouch = false;
        }

        public void Configure(BridgeLayout layout, Vector2 screenCentre, Transform player, int currency, Material material)
        {
            CompleteOnPlayerTouch = false;
            Currency = currency;
            if (Puzzle != null) Destroy(Puzzle.gameObject);
            Puzzle = BridgePuzzle.Create("Trial Bridges", transform, layout, screenCentre, player, FlagColor, material);
            Puzzle.Solved += _ => Complete();
        }
    }
}
