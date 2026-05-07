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
    internal abstract class ItemStoreBase : IItemStore
    {
        public abstract string StoreId { get; }
        public abstract string DisplayName { get; }
        public abstract ItemStoreKind Kind { get; }
        public abstract int Capacity { get; }
        public abstract int Used { get; }
        public virtual bool IsReadOnly { get { return false; } }
        public abstract ItemStoreSnapshot Snapshot();
        public abstract int GetCount(string itemId);
        public abstract bool CanAdd(string itemId, int quantity);
        public abstract bool CanRemove(string itemId, int quantity);
        public abstract ItemTransferResult Add(string itemId, int quantity);
        public abstract ItemTransferResult Remove(string itemId, int quantity);

        protected static bool IsValidQuantity(string itemId, int quantity, out ItemTransferResult result)
        {
            result = null;
            if (string.IsNullOrEmpty(itemId))
            {
                result = ItemTransferResult.Failed(itemId, quantity, "Item ID is required");
                return false;
            }
            if (quantity <= 0)
            {
                result = ItemTransferResult.Failed(itemId, quantity, "Quantity must be greater than zero");
                return false;
            }
            return true;
        }

        protected static ItemStoreItem CreateItem(string itemId, int count)
        {
            return new ItemStoreItem(itemId, StoreItemMetadata.DisplayName(itemId), StoreItemMetadata.Category(itemId), count);
        }
    }
}
