using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A hand-built template for one screen (24x15 since 4.1; the layouts were drawn for 16x10 and scaled by ScreenMath.ContentScale). The prefab root sits at the screen centre; children
    /// hold obstacles and tagged slot markers (exits, gates, hidden items, core entrances).
    /// Built by the Cube > Build Module Library editor script.
    /// </summary>
    public class ScreenModule : MonoBehaviour
    {
        /// <summary>Half-width of the clear lanes through the screen centre (keeps all four exits and the centre open): 1.875, so the lanes and doorways are 3.75 wide.</summary>
        public const float ExitLaneHalfWidth = 1.25f * Game.Tracer.ScreenMath.ContentScale;

        /// <summary>
        /// Clear band along every screen edge: the edge wall (half a tile since 4.1) plus the clear strip inside it (the first
        /// screens' 0.5, scaled), so nothing placed by the KeepsExitsOpen rules meets a wall. 1.25.
        /// </summary>
        public const float EdgeClearance = ScreenEdges.EdgeWallThickness + 0.5f * Game.Tracer.ScreenMath.ContentScale;

        /// <summary>
        /// Rule for every obstacle (local rect, screen centre at the origin): it stays inside the screen,
        /// clear of the edge band and of both centre lanes. Modules that follow it keep all four side exits
        /// open and connected, so connectivity stays a cube-level property.
        /// </summary>
        public static bool KeepsExitsOpen(Rect local)
        {
            Vector2 half = Game.Tracer.ScreenMath.DefaultScreenSize / 2f;
            bool inside = local.xMin >= -half.x + EdgeClearance && local.xMax <= half.x - EdgeClearance &&
                          local.yMin >= -half.y + EdgeClearance && local.yMax <= half.y - EdgeClearance;
            bool clearOfVerticalLane = local.xMin >= ExitLaneHalfWidth || local.xMax <= -ExitLaneHalfWidth;
            bool clearOfHorizontalLane = local.yMin >= ExitLaneHalfWidth || local.yMax <= -ExitLaneHalfWidth;
            return inside && clearOfVerticalLane && clearOfHorizontalLane;
        }

        [SerializeField] private Theme theme;

        [Tooltip("An island screen's module (1.25, Math): open floor with only its exits; MathFace fills it with water and islands.")]
        [SerializeField] private bool islandScreen;

        [Tooltip("A town screen's module (4.5, every face's centre): open floor with only its exits; TownSquareView builds the town on it.")]
        [SerializeField] private bool townScreen;

        public Theme Theme
        {
            get => theme;
            set => theme = value;
        }

        /// <summary>True for an island screen's module: no obstacles, gates, hidden items or core entrances, only its four exits.</summary>
        public bool IsIslandScreen
        {
            get => islandScreen;
            set => islandScreen = value;
        }

        /// <summary>
        /// True for a town screen's module (4.5): no gates, hidden items or core entrances, only its four exits; the town
        /// (TownLayout: buildings, statue, plaza) is built on it when it is placed (TownSquareView), and TownPlan.BlockedRects
        /// counts its solids.
        /// </summary>
        public bool IsTownScreen
        {
            get => townScreen;
            set => townScreen = value;
        }

        public ExitSlot[] Exits => GetComponentsInChildren<ExitSlot>(true);
        public GateSlot[] Gates => GetComponentsInChildren<GateSlot>(true);
        public HiddenItemSlot[] HiddenItems => GetComponentsInChildren<HiddenItemSlot>(true);

        /// <summary>Every core-entrance slot, active or not, in a stable (hierarchy) order.</summary>
        public CoreEntranceSlot[] CoreEntrances => GetComponentsInChildren<CoreEntranceSlot>(true);

        public int CoreEntranceCount => CoreEntrances.Length;

        /// <summary>Number of gate slots (each with its alcove), in a stable (hierarchy) order.</summary>
        public int GateSlotCount => Gates.Length;

        /// <summary>Every slot of every kind, active or not.</summary>
        public ModuleSlot[] AllSlots => GetComponentsInChildren<ModuleSlot>(true);

        /// <summary>Activates only the core-entrance slot with the given index; -1 deactivates all of them.</summary>
        public void SetActiveCoreEntrance(int index)
        {
            CoreEntranceSlot[] slots = CoreEntrances;
            for (int i = 0; i < slots.Length; i++)
                slots[i].gameObject.SetActive(i == index);
        }

        /// <summary>The currently active core-entrance slots (0 or 1 once laid out).</summary>
        public List<CoreEntranceSlot> ActiveCoreEntrances()
        {
            var active = new List<CoreEntranceSlot>();
            foreach (CoreEntranceSlot slot in CoreEntrances)
                if (slot.gameObject.activeSelf) active.Add(slot);
            return active;
        }
    }
}
