using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A bridge door: a door across an alcove opening, linked to an island-and-bridge layout (BridgePuzzle) on the same
    /// screen. It opens for good, with the gate-open effect, when that layout is walked crossing every bridge exactly once
    /// (no item needed); touches and swings do nothing. A flag-coloured "Bridge Mark" matches the flag of its layout.
    /// </summary>
    public class BridgeDoorGate : Gate
    {
        [SerializeField] private BridgePuzzle puzzle;

        protected override bool OpensOnTouch => false;

        public BridgePuzzle Puzzle => puzzle;

        /// <summary>The number of Math gates on the Math face (the difficulty: it sets the layout's bridges).</summary>
        public int Difficulty { get; set; } = 1;

        protected override Color ClosedColor => puzzle != null ? Color.Lerp(Color.white, puzzle.Accent, 0.5f) : Color.white;

        public void Link(BridgePuzzle linked)
        {
            if (puzzle != null) puzzle.Solved -= OnSolved;
            puzzle = linked;
            if (puzzle != null)
            {
                puzzle.Solved += OnSolved;
                if (puzzle.IsSolved) OpenNow();
            }
            ApplyVisual();
        }

        private void OnSolved(BridgePuzzle solved) => OpenNow();

        protected override void OnOpened()
        {
            Transform mark = transform.Find("Bridge Mark");
            if (mark != null) mark.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (puzzle != null) puzzle.Solved -= OnSolved;
        }

        public static BridgeDoorGate Build(GameObject block, ItemDefinition item, BridgePuzzle puzzle, int difficulty, Material material)
        {
            block.name = $"Bridge Door ({item})";
            var gate = block.AddComponent<BridgeDoorGate>();
            gate.Difficulty = Mathf.Max(1, difficulty);
            gate.Visual = block.GetComponentInChildren<SpriteRenderer>();
            gate.RequiredItem = item;
            Color flag = puzzle != null ? puzzle.Accent : Color.white;
            BeakGate.AddSprite(block.transform, "Bridge Mark", PartShape.Triangle, Vector2.zero, new Vector2(0.34f, 0.3f), flag, -3, material);
            gate.Link(puzzle);
            ArtCatalog.DressGate(gate, ArtKey.GateBridgeDoor);
            return gate;
        }
    }
}
