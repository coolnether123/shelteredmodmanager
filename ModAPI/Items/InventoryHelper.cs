using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ModAPI.Content;
using UnityEngine;

namespace ModAPI.Items
{
    /// <summary>
    /// Safe item operations for mod developers.
    /// Provides high-level methods for creating, adding, and removing items without 
    /// needing to interact with the game's internal manager logic directly.
    /// </summary>
    public static class InventoryHelper
    {
        private static InventoryManager Mgr => InventoryManager.Instance;

        /// <summary>
        /// Resolve a string ID (vanilla or modded) to the game's internal ItemType.
        /// </summary>
        public static bool ResolveItemType(string itemId, out ItemManager.ItemType type)
        {
            return ContentInjector.ResolveItemType(itemId, out type);
        }

        /// <summary>
        /// Create a new ItemInstance for the given item ID.
        /// Note: This does NOT add the item to any inventory.
        /// </summary>
        public static ItemInstance CreateItem(string itemId)
        {
            if (ResolveItemType(itemId, out var type))
            {
                return new ItemInstance(type);
            }
            return null;
        }

        /// <summary>
        /// Try to add an item instance to the primary shelter inventory.
        /// Accounts for storage capacity and special item logic (food, water, etc).
        /// </summary>
        public static bool TryAddToInventory(ItemInstance item)
        {
            if (Mgr == null || item == null) return false;
            return Mgr.AddExistingItem(item);
        }

        /// <summary>
        /// Try to add a quantity of a specific item to the primary shelter inventory.
        /// </summary>
        public static bool TryAddToInventory(string itemId, int quantity = 1)
        {
            if (Mgr == null || quantity <= 0) return false;
            if (ResolveItemType(itemId, out var type))
            {
                return Mgr.AddNewItems(type, quantity);
            }
            return false;
        }

        /// <summary>
        /// Try to remove a quantity of a specific item from the primary shelter inventory.
        /// </summary>
        public static bool TryRemoveFromInventory(string itemId, int quantity = 1)
        {
            if (Mgr == null || quantity <= 0) return false;
            if (ResolveItemType(itemId, out var type))
            {
                return Mgr.RemoveItemsOfType(type, quantity);
            }
            return false;
        }

        /// <summary>
        /// Get the total count of a specific item in the shelter inventory.
        /// </summary>
        public static int GetItemCount(string itemId, bool includeParties = false)
        {
            if (Mgr == null) return 0;
            if (ResolveItemType(itemId, out var type))
            {
                return Mgr.GetItemCountInStorage(type, includeParties);
            }
            return 0;
        }

        /// <summary>
        /// Get a list of all item stacks currently in the shelter inventory.
        /// </summary>
        public static ReadOnlyCollection<ItemStack> GetAllItems()
        {
            if (Mgr == null) return new List<ItemStack>().AsReadOnly();
            return Mgr.GetItems().AsReadOnly();
        }

        /// <summary>
        /// Get the total storage capacity of the shelter (number of stacks).
        /// </summary>
        public static int GetStorageCapacity()
        {
            return Mgr != null ? Mgr.storageCapacity : 0;
        }

        /// <summary>
        /// Get the number of used storage slots (number of stacks).
        /// </summary>
        public static int GetUsedStorage()
        {
            return Mgr != null ? Mgr.GetTotalStackCount() : 0;
        }
    }
}
