using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Cube
{
    /// <summary>
    /// The item the player attacks with, chosen from the Inventory. Player/Previous and Player/Next cycle
    /// through owned items (nothing happens with none); the first item picked up is equipped automatically.
    /// Null means bare-handed.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class Equipment : MonoBehaviour
    {
        [SerializeField] private string previousActionPath = "Player/Previous";
        [SerializeField] private string nextActionPath = "Player/Next";

        private Inventory inventory;
        private InputAction previous;
        private InputAction next;

        /// <summary>Raised when the equipped item changes, with the new one (null = bare hands).</summary>
        public event Action<ItemDefinition> Changed;

        public ItemDefinition Equipped { get; private set; }

        public Inventory Inventory => inventory != null ? inventory : inventory = GetComponent<Inventory>();

        private void OnEnable()
        {
            previous = NpcInput.Find(previousActionPath, this);
            next = NpcInput.Find(nextActionPath, this);
            Inventory.ItemAdded += OnItemAdded;
            if (Equipped == null && Inventory.Items.Count > 0) Equip(Inventory.Items[0]);
        }

        private void OnDisable()
        {
            previous = null; // shared project-wide actions: never disabled here
            next = null;
            if (inventory != null) inventory.ItemAdded -= OnItemAdded;
        }

        private void Update()
        {
            if (Equipped != null && !Inventory.Has(Equipped)) Equip(Inventory.Items.Count > 0 ? Inventory.Items[0] : null);
            if (next != null && next.WasPressedThisFrame()) Cycle(1);
            if (previous != null && previous.WasPressedThisFrame()) Cycle(-1);
        }

        /// <summary>Moves the equipped item by step through the owned items, wrapping. No items: nothing happens.</summary>
        public void Cycle(int step)
        {
            var items = Inventory.Items;
            if (items.Count == 0) return;
            int index = Equipped != null ? IndexOf(Equipped) : -1;
            if (index < 0) index = step > 0 ? -1 : 0;
            int count = items.Count;
            Equip(items[((index + step) % count + count) % count]);
        }

        /// <summary>Equips an owned item (or null for bare hands). Returns false for an item not owned.</summary>
        public bool Equip(ItemDefinition item)
        {
            if (item != null && !Inventory.Has(item)) return false;
            if (Equipped == item) return true;
            Equipped = item;
            Changed?.Invoke(item);
            return true;
        }

        private int IndexOf(ItemDefinition item)
        {
            var items = Inventory.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i] == item) return i;
            return -1;
        }

        private void OnItemAdded(ItemDefinition item)
        {
            if (Equipped == null) Equip(item);
        }
    }
}
