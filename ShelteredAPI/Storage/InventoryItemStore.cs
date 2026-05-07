using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Persistence;
using ShelteredAPI.UI.Runtime;
using UnityEngine;
using GameItemDefinition = global::ItemDefinition;

namespace ShelteredAPI.Storage
{
    internal sealed class InventoryItemStore : ItemStoreBase
    {
        public override string StoreId { get { return "sheltered.inventory"; } }
        public override string DisplayName { get { return "Shelter Inventory"; } }
        public override ItemStoreKind Kind { get { return ItemStoreKind.Inventory; } }
        public override int Capacity { get { return ShelteredContent.GetStorageCapacity(); } }
        public override int Used { get { return ShelteredContent.GetUsedStorage(); } }

        public override ItemStoreSnapshot Snapshot()
        {
            ItemStoreSnapshot snapshot = new ItemStoreSnapshot
            {
                StoreId = StoreId,
                DisplayName = DisplayName,
                Kind = Kind,
                Capacity = Capacity,
                Used = Used
            };

            AddSpecialFood(snapshot.Items, VanillaItems.Ration, FoodManager.Instance != null ? FoodManager.Instance.Rations : 0);
            AddSpecialFood(snapshot.Items, VanillaItems.Meat, FoodManager.Instance != null ? FoodManager.Instance.Meat : 0);
            AddSpecialFood(snapshot.Items, VanillaItems.DesperateMeat, FoodManager.Instance != null ? FoodManager.Instance.DesperateMeat : 0);

            IList<ItemStack> stacks = ShelteredContent.GetAllInventoryItems();
            for (int i = 0; stacks != null && i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.m_count <= 0)
                    continue;
                string itemId = stack.m_type.ToString();
                if (IsSpecialFood(itemId))
                    continue;
                snapshot.Items.Add(CreateItem(itemId, stack.m_count));
            }

            return snapshot;
        }

        public override int GetCount(string itemId)
        {
            if (IsRation(itemId))
                return FoodManager.Instance != null ? FoodManager.Instance.Rations : 0;
            if (IsMeat(itemId))
                return FoodManager.Instance != null ? FoodManager.Instance.Meat : 0;
            if (IsDesperateMeat(itemId))
                return FoodManager.Instance != null ? FoodManager.Instance.DesperateMeat : 0;
            return ShelteredContent.GetItemCount(itemId);
        }

        public override bool CanAdd(string itemId, int quantity)
        {
            if (quantity <= 0 || string.IsNullOrEmpty(itemId))
                return false;
            if (IsRation(itemId))
                return FoodManager.Instance != null;
            if (IsMeat(itemId) || IsDesperateMeat(itemId))
                return FoodManager.Instance != null;
            return true;
        }

        public override bool CanRemove(string itemId, int quantity)
        {
            return quantity > 0 && GetCount(itemId) >= quantity;
        }

        public override ItemTransferResult Add(string itemId, int quantity)
        {
            ItemTransferResult validation;
            if (!IsValidQuantity(itemId, quantity, out validation))
                return validation;

            if (IsRation(itemId))
            {
                if (FoodManager.Instance == null)
                    return ItemTransferResult.Failed(itemId, quantity, "FoodManager is not available");
                bool accepted = FoodManager.Instance.AddRations(quantity);
                return accepted ? ItemTransferResult.Ok(itemId, quantity, quantity) : ItemTransferResult.Failed(itemId, quantity, "Pantry capacity rejected some rations");
            }
            if (IsMeat(itemId))
            {
                int moved = FoodManager.Instance != null ? FoodManager.Instance.AddMeat(quantity) : 0;
                return moved > 0 ? ItemTransferResult.Ok(itemId, quantity, moved) : ItemTransferResult.Failed(itemId, quantity, "No freezer capacity for meat");
            }
            if (IsDesperateMeat(itemId))
            {
                int moved = FoodManager.Instance != null ? FoodManager.Instance.AddDesperateMeat(quantity) : 0;
                return moved > 0 ? ItemTransferResult.Ok(itemId, quantity, moved) : ItemTransferResult.Failed(itemId, quantity, "No freezer capacity for desperate meat");
            }

            InventoryMutationResult result = ShelteredContent.AddToInventory(itemId, quantity);
            return result.Success
                ? ItemTransferResult.Ok(itemId, quantity, quantity)
                : ItemTransferResult.Failed(itemId, quantity, result.ErrorMessage);
        }

        public override ItemTransferResult Remove(string itemId, int quantity)
        {
            ItemTransferResult validation;
            if (!IsValidQuantity(itemId, quantity, out validation))
                return validation;
            if (!CanRemove(itemId, quantity))
                return ItemTransferResult.Failed(itemId, quantity, "Store does not contain enough items");

            if (IsRation(itemId))
                return ItemTransferResult.Ok(itemId, quantity, FoodManager.Instance.TakeRations(quantity));
            if (IsMeat(itemId))
                return ItemTransferResult.Ok(itemId, quantity, FoodManager.Instance.TakeMeat(quantity));
            if (IsDesperateMeat(itemId))
                return ItemTransferResult.Ok(itemId, quantity, FoodManager.Instance.TakeDesperateMeat(quantity));

            InventoryMutationResult result = ShelteredContent.RemoveFromInventory(itemId, quantity);
            return result.Success
                ? ItemTransferResult.Ok(itemId, quantity, quantity)
                : ItemTransferResult.Failed(itemId, quantity, result.ErrorMessage);
        }

        private static void AddSpecialFood(IList<ItemStoreItem> items, string itemId, int count)
        {
            if (items != null && count > 0)
                items.Add(CreateItem(itemId, count));
        }

        internal static bool IsSpecialFood(string itemId)
        {
            return IsRation(itemId) || IsMeat(itemId) || IsDesperateMeat(itemId);
        }

        internal static bool IsRation(string itemId)
        {
            return string.Equals(itemId, VanillaItems.Ration, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsMeat(string itemId)
        {
            return string.Equals(itemId, VanillaItems.Meat, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDesperateMeat(string itemId)
        {
            return string.Equals(itemId, VanillaItems.DesperateMeat, StringComparison.OrdinalIgnoreCase);
        }
    }
}
