using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// A hand-built template for one 16x10 screen. The prefab root sits at the screen centre; children
    /// hold obstacles and tagged slot markers (exits, gates, hidden items, core entrances).
    /// Built by the Cube > Build Module Library editor script.
    /// </summary>
    public class ScreenModule : MonoBehaviour
    {
        /// <summary>Half-width of the clear lanes through the screen centre (keeps all four exits and the centre open).</summary>
        public const float ExitLaneHalfWidth = 1.25f;

        /// <summary>Clear band along every screen edge, so the whole edge stays crossable.</summary>
        public const float EdgeClearance = 1.5f;

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

        public Theme Theme
        {
            get => theme;
            set => theme = value;
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
