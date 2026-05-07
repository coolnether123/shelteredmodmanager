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
    /// <summary>
    /// Public facade for vanilla and mod-owned item stores.
    /// </summary>
    public static class ShelteredStores
    {
        static ShelteredStores()
        {
            EnsurePersistenceRegistered();
        }

        internal static void EnsurePersistenceRegistered()
        {
            ModItemStorePersistence.EnsureRegistered();
        }

        public static IItemStore ForInventory()
        {
            return new InventoryItemStore();
        }

        public static IItemStore ForFreezer(Obj_Freezer freezer)
        {
            return new FreezerItemStore(freezer);
        }

        public static IItemStore ForMod(string ownerId, string storeId, string displayName)
        {
            return ModItemStoreRegistry.Get(ownerId, storeId, displayName, -1);
        }

        public static IItemStore ForMod(string ownerId, string storeId, string displayName, int capacity)
        {
            return ModItemStoreRegistry.Get(ownerId, storeId, displayName, capacity);
        }

        public static IItemStore ForObject(string ownerId, Obj_Base targetObject, string displayName)
        {
            return ForObject(ownerId, targetObject, displayName, -1);
        }

        public static IItemStore ForObject(string ownerId, Obj_Base targetObject, string displayName, int capacity)
        {
            if (targetObject == null)
                throw new ArgumentNullException("targetObject");

            return ForMod(ownerId, BuildObjectStoreId(targetObject), displayName, capacity);
        }

        public static IItemStore FindNearestObjectStore(string ownerId, ObjectManager.ObjectType objectType, Vector3 position, string displayName)
        {
            return FindNearestObjectStore(ownerId, objectType, position, displayName, -1);
        }

        public static IItemStore FindNearestObjectStore(string ownerId, ObjectManager.ObjectType objectType, Vector3 position, string displayName, int capacity)
        {
            Obj_Base target = FindNearestObject(objectType, position);
            return target != null ? ForObject(ownerId, target, displayName, capacity) : null;
        }

        public static Obj_Base FindNearestObject(ObjectManager.ObjectType objectType, Vector3 position)
        {
            if (ObjectManager.Instance == null || objectType == ObjectManager.ObjectType.Undefined || objectType == ObjectManager.ObjectType.Max)
                return null;

            List<Obj_Base> objects;
            try
            {
                objects = ObjectManager.Instance.GetNearestObjectsOfType(objectType, position);
            }
            catch
            {
                return null;
            }

            for (int i = 0; objects != null && i < objects.Count; i++)
            {
                if (objects[i] != null)
                    return objects[i];
            }

            return null;
        }

        public static IItemStore FindNearestFreezer(Vector3 position)
        {
            Obj_Freezer freezer = FindNearestFreezerObject(position);
            return freezer != null ? ForFreezer(freezer) : null;
        }

        public static IList<IItemStore> GetFreezers()
        {
            List<IItemStore> stores = new List<IItemStore>();
            if (ObjectManager.Instance == null)
                return stores;

            List<Obj_Base> objects = ObjectManager.Instance.GetObjectsOfType(ObjectManager.ObjectType.Freezer);
            for (int i = 0; objects != null && i < objects.Count; i++)
            {
                Obj_Freezer freezer = objects[i] as Obj_Freezer;
                if (freezer != null)
                    stores.Add(ForFreezer(freezer));
            }

            return stores;
        }

        public static Obj_Freezer FindNearestFreezerObject(Vector3 position)
        {
            Obj_Base target = FindNearestObject(ObjectManager.ObjectType.Freezer, position);
            return target as Obj_Freezer;
        }

        internal static bool TryResolveStore(string storeId, ItemStoreKind kind, out IItemStore store)
        {
            store = null;
            if (string.IsNullOrEmpty(storeId))
                return false;

            if (string.Equals(storeId, "sheltered.inventory", StringComparison.OrdinalIgnoreCase))
            {
                store = ForInventory();
                return true;
            }

            if (kind == ItemStoreKind.Freezer && storeId.StartsWith("sheltered.freezer.", StringComparison.OrdinalIgnoreCase))
            {
                int objectId;
                string idText = storeId.Substring("sheltered.freezer.".Length);
                if (int.TryParse(idText, out objectId) && ObjectManager.Instance != null)
                {
                    Obj_Freezer freezer = ObjectManager.Instance.GetObjectWithId(objectId) as Obj_Freezer;
                    if (freezer != null)
                    {
                        store = ForFreezer(freezer);
                        return true;
                    }
                }
            }

            if (kind == ItemStoreKind.Mod)
                return ModItemStoreRegistry.TryGet(storeId, out store);

            return false;
        }

        public static ItemTransferResult Transfer(IItemStore source, IItemStore target, string itemId, int quantity)
        {
            if (source == null)
                return ItemTransferResult.Failed(itemId, quantity, "Source store is required");
            if (target == null)
                return ItemTransferResult.Failed(itemId, quantity, "Target store is required");
            if (string.IsNullOrEmpty(itemId))
                return ItemTransferResult.Failed(itemId, quantity, "Item ID is required");
            if (quantity <= 0)
                return ItemTransferResult.Failed(itemId, quantity, "Quantity must be greater than zero");
            if (!source.CanRemove(itemId, quantity))
                return ItemTransferResult.Failed(itemId, quantity, "Source store does not contain enough items");
            if (!target.CanAdd(itemId, quantity))
                return ItemTransferResult.Failed(itemId, quantity, GetCannotAddMessage(target, itemId));

            ItemTransferResult removed = source.Remove(itemId, quantity);
            if (!removed.Success)
                return removed;

            ItemTransferResult added = target.Add(itemId, removed.Moved);
            if (added.Success && added.Moved == removed.Moved)
                return ItemTransferResult.Ok(itemId, quantity, added.Moved);

            RollbackTransfer(source, target, itemId, removed.Moved, added);
            return ItemTransferResult.Failed(itemId, quantity, added.ErrorMessage ?? "Target store rejected transfer and the transfer was rolled back");
        }

        public static IList<ContainerUiItem> ToContainerItems(IItemStore store)
        {
            List<ContainerUiItem> items = new List<ContainerUiItem>();
            if (store == null)
                return items;

            ItemStoreSnapshot snapshot = store.Snapshot();
            if (snapshot == null || snapshot.Items == null)
                return items;

            for (int i = 0; i < snapshot.Items.Count; i++)
            {
                ItemStoreItem item = snapshot.Items[i];
                if (item == null || item.Count <= 0)
                    continue;

                items.Add(new ContainerUiItem(item.ItemId, item.DisplayName, item.Category, item.Count)
                {
                    Subtitle = item.Subtitle
                });
            }

            return items;
        }

        public static ContainerUiRequest CreateContainerRequest(IItemStore store, string ownerId, string panelId, string title)
        {
            return CreateContainerRequest(store, ForInventory(), ownerId, panelId, title);
        }

        public static ContainerUiRequest CreateContainerRequest(IItemStore store, IItemStore transferStore, string ownerId, string panelId, string title)
        {
            if (store == null)
                throw new ArgumentNullException("store");

            ContainerUiRequest request = new ContainerUiRequest();
            request.PanelId = panelId;
            request.Title = string.IsNullOrEmpty(title) ? store.DisplayName : title;
            request.OwnerId = ownerId;
            request.EmptyText = "No items";
            request.ItemSource = delegate { return ToContainerItems(store); };
            request.CanTransfer = item => CanTransferContainerItem(store, transferStore, item, request.TransferDirection, request.TransferQuantity);
            request.OnTransferRequested = transfer =>
            {
                TransferContainerItem(store, transferStore, transfer);
            };
            return request;
        }

        public static string BuildObjectStoreId(Obj_Base targetObject)
        {
            if (targetObject == null)
                return "object.missing";

            string objectType = "Unknown";
            try
            {
                objectType = targetObject.GetObjectType().ToString();
            }
            catch
            {
            }

            return "object." + objectType + "." + targetObject.objectId;
        }

        private static bool CanTransferContainerItem(IItemStore store, IItemStore transferStore, ContainerUiItem item, ContainerUiTransferDirection direction, int requestedQuantity)
        {
            if (store == null || transferStore == null || item == null || string.IsNullOrEmpty(item.ItemId) || item.Count <= 0)
                return false;

            int quantity = Math.Max(1, requestedQuantity);
            if (direction == ContainerUiTransferDirection.IntoContainer)
                return transferStore.CanRemove(item.ItemId, quantity) && store.CanAdd(item.ItemId, quantity);

            return store.CanRemove(item.ItemId, quantity) && transferStore.CanAdd(item.ItemId, quantity);
        }

        private static ItemTransferResult TransferContainerItem(IItemStore store, IItemStore transferStore, ContainerUiTransferContext transfer)
        {
            if (transfer == null || transfer.Item == null)
                return ItemTransferResult.Failed(null, 0, "Transfer item is required");

            int quantity = Math.Max(1, transfer.Quantity);
            if (transfer.Direction == ContainerUiTransferDirection.IntoContainer)
                return Transfer(transferStore, store, transfer.Item.ItemId, quantity);

            return Transfer(store, transferStore, transfer.Item.ItemId, quantity);
        }

        private static string GetCannotAddMessage(IItemStore target, string itemId)
        {
            FreezerItemStore freezer = target as FreezerItemStore;
            if (freezer != null && !FreezerItemStore.IsFreezerItem(itemId))
                return "Vanilla freezers only accept Meat and DesperateMeat. Use ShelteredStores.ForMod or ShelteredStores.ForObject for custom item IDs.";

            return "Target store cannot accept the requested items";
        }

        private static void RollbackTransfer(IItemStore source, IItemStore target, string itemId, int removedCount, ItemTransferResult addResult)
        {
            if (source == null || target == null || string.IsNullOrEmpty(itemId) || removedCount <= 0)
                return;

            int targetMoved = addResult != null ? Math.Max(0, addResult.Moved) : 0;
            if (targetMoved > 0)
            {
                ItemTransferResult targetRollback = target.Remove(itemId, targetMoved);
                if (targetRollback.Success && targetRollback.Moved > 0)
                    source.Add(itemId, targetRollback.Moved);
            }

            int notAdded = Math.Max(0, removedCount - targetMoved);
            if (notAdded > 0)
                source.Add(itemId, notAdded);
        }
    }
}
