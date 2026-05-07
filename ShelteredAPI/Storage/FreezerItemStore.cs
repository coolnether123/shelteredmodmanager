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
    internal sealed class FreezerItemStore : ItemStoreBase
    {
        private readonly Obj_Freezer _freezer;

        public FreezerItemStore(Obj_Freezer freezer)
        {
            _freezer = freezer;
        }

        public override string StoreId
        {
            get { return _freezer != null ? "sheltered.freezer." + _freezer.objectId : "sheltered.freezer.missing"; }
        }

        public override string DisplayName { get { return "Freezer"; } }
        public override ItemStoreKind Kind { get { return ItemStoreKind.Freezer; } }
        public override int Capacity { get { return _freezer != null ? _freezer.TotalMeatCapacity : 0; } }
        public override int Used { get { return _freezer != null ? _freezer.Meat + _freezer.DesperateMeat : 0; } }

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

            if (_freezer == null)
                return snapshot;

            if (_freezer.Meat > 0)
                snapshot.Items.Add(CreateItem(VanillaItems.Meat, _freezer.Meat));
            if (_freezer.DesperateMeat > 0)
                snapshot.Items.Add(CreateItem(VanillaItems.DesperateMeat, _freezer.DesperateMeat));
            return snapshot;
        }

        public override int GetCount(string itemId)
        {
            if (_freezer == null)
                return 0;
            if (InventoryItemStore.IsMeat(itemId))
                return _freezer.Meat;
            if (InventoryItemStore.IsDesperateMeat(itemId))
                return _freezer.DesperateMeat;
            return 0;
        }

        public override bool CanAdd(string itemId, int quantity)
        {
            return _freezer != null
                && quantity > 0
                && IsFreezerItem(itemId)
                && _freezer.TotalSpaceAvailable() >= quantity;
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
            if (_freezer == null)
                return ItemTransferResult.Failed(itemId, quantity, "Freezer is no longer available");

            int moved = 0;
            if (InventoryItemStore.IsMeat(itemId))
                moved = _freezer.AddMeat(quantity);
            else if (InventoryItemStore.IsDesperateMeat(itemId))
                moved = _freezer.AddDesperateMeat(quantity);
            else
                return ItemTransferResult.Failed(itemId, quantity, "Vanilla freezers only accept Meat and DesperateMeat. Use a mod-owned store for custom item IDs.");

            return moved > 0 ? ItemTransferResult.Ok(itemId, quantity, moved) : ItemTransferResult.Failed(itemId, quantity, "Freezer has no available capacity");
        }

        public override ItemTransferResult Remove(string itemId, int quantity)
        {
            ItemTransferResult validation;
            if (!IsValidQuantity(itemId, quantity, out validation))
                return validation;
            if (_freezer == null)
                return ItemTransferResult.Failed(itemId, quantity, "Freezer is no longer available");
            if (!CanRemove(itemId, quantity))
                return ItemTransferResult.Failed(itemId, quantity, "Freezer does not contain enough items");

            int moved = InventoryItemStore.IsMeat(itemId)
                ? _freezer.RemoveMeat(quantity)
                : _freezer.RemoveDesperateMeat(quantity);
            return ItemTransferResult.Ok(itemId, quantity, moved);
        }

        internal static bool IsFreezerItem(string itemId)
        {
            return InventoryItemStore.IsMeat(itemId) || InventoryItemStore.IsDesperateMeat(itemId);
        }
    }
}
