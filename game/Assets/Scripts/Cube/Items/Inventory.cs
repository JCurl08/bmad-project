using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Cube
{
    /// <summary>
    /// The player's items. Lives on the player; pickups add to it and gates read it. Most items stay for the
    /// whole run; a consumable one (the Chemistry isotope's lead, dropped on a plate) is removed with Remove.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        private readonly List<ItemDefinition> items = new List<ItemDefinition>();

        /// <summary>Raised once per newly added item.</summary>
        public event Action<ItemDefinition> ItemAdded;

        /// <summary>Raised once per removed item.</summary>
        public event Action<ItemDefinition> ItemRemoved;

        public IReadOnlyList<ItemDefinition> Items => items;

        public bool Has(ItemDefinition item) => item != null && items.Contains(item);

        /// <summary>Adds an item. Returns false (and raises nothing) if it is null or already held.</summary>
        public bool Add(ItemDefinition item)
        {
            if (item == null || items.Contains(item)) return false;
            items.Add(item);
            ItemAdded?.Invoke(item);
            return true;
        }

        /// <summary>Removes a held item (a consumed one). Returns false (and raises nothing) if it was not held.</summary>
        public bool Remove(ItemDefinition item)
        {
            if (item == null || !items.Remove(item)) return false;
            ItemRemoved?.Invoke(item);
            return true;
        }

        /// <summary>Empties the inventory (a new run).</summary>
        public void Clear() => items.Clear();
    }
}
