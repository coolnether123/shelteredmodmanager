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
                return ItemTransferResult.Failed(itemId, quantity, "Target store cannot accept the requested items");

            ItemTransferResult removed = source.Remove(itemId, quantity);
            if (!removed.Success)
                return removed;

            ItemTransferResult added = target.Add(itemId, removed.Moved);
            if (added.Success && added.Moved == removed.Moved)
                return ItemTransferResult.Ok(itemId, quantity, added.Moved);

            if (added.Moved > 0 && added.Moved < removed.Moved)
                source.Add(itemId, removed.Moved - added.Moved);
            else if (added.Moved <= 0)
                source.Add(itemId, removed.Moved);

            return ItemTransferResult.Failed(itemId, quantity, added.ErrorMessage ?? "Target store rejected transfer");
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
                    Subtitle = item.Subtitle,
                    Tag = item.Tag
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
    }

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
                && (InventoryItemStore.IsMeat(itemId) || InventoryItemStore.IsDesperateMeat(itemId))
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
                return ItemTransferResult.Failed(itemId, quantity, "Freezers only accept meat and desperate meat");

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
    }

    internal sealed class ModItemStore : ItemStoreBase
    {
        private readonly ModItemStoreState _state;

        internal ModItemStore(ModItemStoreState state)
        {
            _state = state;
        }

        public override string StoreId { get { return _state.StoreId; } }
        public override string DisplayName { get { return _state.DisplayName; } }
        public override ItemStoreKind Kind { get { return ItemStoreKind.Mod; } }
        public override int Capacity { get { return _state.Capacity; } }
        public override int Used { get { return _state.Used; } }

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

            foreach (KeyValuePair<string, int> pair in _state.Items)
            {
                if (pair.Value > 0)
                    snapshot.Items.Add(CreateItem(pair.Key, pair.Value));
            }

            return snapshot;
        }

        public override int GetCount(string itemId)
        {
            int count;
            return !string.IsNullOrEmpty(itemId) && _state.Items.TryGetValue(itemId, out count) ? count : 0;
        }

        public override bool CanAdd(string itemId, int quantity)
        {
            return !string.IsNullOrEmpty(itemId)
                && quantity > 0
                && (_state.Capacity <= 0 || _state.Used + quantity <= _state.Capacity);
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
            if (!CanAdd(itemId, quantity))
                return ItemTransferResult.Failed(itemId, quantity, "Store capacity would be exceeded");

            int count = GetCount(itemId);
            _state.Items[itemId] = count + quantity;
            return ItemTransferResult.Ok(itemId, quantity, quantity);
        }

        public override ItemTransferResult Remove(string itemId, int quantity)
        {
            ItemTransferResult validation;
            if (!IsValidQuantity(itemId, quantity, out validation))
                return validation;
            if (!CanRemove(itemId, quantity))
                return ItemTransferResult.Failed(itemId, quantity, "Store does not contain enough items");

            int count = GetCount(itemId) - quantity;
            if (count > 0)
                _state.Items[itemId] = count;
            else
                _state.Items.Remove(itemId);
            return ItemTransferResult.Ok(itemId, quantity, quantity);
        }
    }

    internal sealed class ModItemStoreState
    {
        public string OwnerId;
        public string StoreId;
        public string DisplayName;
        public int Capacity;
        public readonly Dictionary<string, int> Items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int Used
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, int> pair in Items)
                    total += Math.Max(0, pair.Value);
                return total;
            }
        }
    }

    internal static class ModItemStoreRegistry
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, ModItemStoreState> Stores = new Dictionary<string, ModItemStoreState>(StringComparer.OrdinalIgnoreCase);

        public static IItemStore Get(string ownerId, string storeId, string displayName, int capacity)
        {
            if (string.IsNullOrEmpty(ownerId))
                ownerId = "anonymous";
            if (string.IsNullOrEmpty(storeId))
                storeId = "default";

            ModItemStorePersistence.EnsureRegistered();
            string key = BuildKey(ownerId, storeId);
            lock (Sync)
            {
                ModItemStoreState state;
                if (!Stores.TryGetValue(key, out state))
                {
                    state = new ModItemStoreState
                    {
                        OwnerId = ownerId,
                        StoreId = key,
                        DisplayName = string.IsNullOrEmpty(displayName) ? storeId : displayName,
                        Capacity = Math.Max(0, capacity)
                    };
                    Stores[key] = state;
                }
                else
                {
                    if (!string.IsNullOrEmpty(displayName))
                        state.DisplayName = displayName;
                    if (capacity >= 0)
                        state.Capacity = capacity;
                }

                return new ModItemStore(state);
            }
        }

        internal static List<ModItemStoreState> Snapshot()
        {
            lock (Sync)
                return new List<ModItemStoreState>(Stores.Values);
        }

        internal static void ReplaceAll(List<ModItemStoreState> states)
        {
            lock (Sync)
            {
                Stores.Clear();
                for (int i = 0; states != null && i < states.Count; i++)
                {
                    ModItemStoreState state = states[i];
                    if (state != null && !string.IsNullOrEmpty(state.StoreId))
                        Stores[state.StoreId] = state;
                }
            }
        }

        internal static bool TryGet(string storeId, out IItemStore store)
        {
            store = null;
            if (string.IsNullOrEmpty(storeId))
                return false;

            lock (Sync)
            {
                ModItemStoreState state;
                if (!Stores.TryGetValue(storeId, out state))
                    return false;

                store = new ModItemStore(state);
                return true;
            }
        }

        private static string BuildKey(string ownerId, string storeId)
        {
            return ownerId + "." + storeId;
        }
    }

    internal sealed class ModItemStorePersistence : ISaveable
    {
        private const string GroupName = "ShelteredAPI_ModItemStores";
        private const string StoresKey = "stores";
        private static readonly ModItemStorePersistence Instance = new ModItemStorePersistence();
        private static bool _registered;

        private ModItemStorePersistence()
        {
        }

        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            ModPersistence.Register(Instance);
            _registered = true;
        }

        public bool IsReadyForLoad() { return true; }
        public bool IsRelocationEnabled() { return true; }

        public bool SaveLoad(SaveData data)
        {
            if (data == null)
                return false;

            data.GroupStart(GroupName);
            try
            {
                List<StoreSaveEntry> entries = data.isSaving ? BuildEntries(ModItemStoreRegistry.Snapshot()) : new List<StoreSaveEntry>();
                data.SaveLoadList(StoresKey, (IList)entries,
                    i => SaveLoadEntry(data, entries[i]),
                    i =>
                    {
                        StoreSaveEntry entry = new StoreSaveEntry();
                        SaveLoadEntry(data, entry);
                        entries.Add(entry);
                    });

                if (data.isLoading)
                    ModItemStoreRegistry.ReplaceAll(BuildStates(entries));
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ModItemStorePersistence.SaveLoad", "Store persistence failed: " + ex.Message);
            }
            finally
            {
                data.GroupEnd();
            }

            return true;
        }

        private static void SaveLoadEntry(SaveData data, StoreSaveEntry entry)
        {
            data.GroupStart("store");
            data.SaveLoad("ownerId", ref entry.OwnerId);
            data.SaveLoad("storeId", ref entry.StoreId);
            data.SaveLoad("displayName", ref entry.DisplayName);
            data.SaveLoad("capacity", ref entry.Capacity);
            data.SaveLoad("itemId", ref entry.ItemId);
            data.SaveLoad("count", ref entry.Count);
            data.GroupEnd();
        }

        private static List<StoreSaveEntry> BuildEntries(List<ModItemStoreState> states)
        {
            List<StoreSaveEntry> entries = new List<StoreSaveEntry>();
            for (int i = 0; states != null && i < states.Count; i++)
            {
                ModItemStoreState state = states[i];
                if (state == null)
                    continue;

                bool any = false;
                foreach (KeyValuePair<string, int> item in state.Items)
                {
                    any = true;
                    entries.Add(new StoreSaveEntry
                    {
                        OwnerId = state.OwnerId,
                        StoreId = state.StoreId,
                        DisplayName = state.DisplayName,
                        Capacity = state.Capacity,
                        ItemId = item.Key,
                        Count = item.Value
                    });
                }

                if (!any)
                {
                    entries.Add(new StoreSaveEntry
                    {
                        OwnerId = state.OwnerId,
                        StoreId = state.StoreId,
                        DisplayName = state.DisplayName,
                        Capacity = state.Capacity
                    });
                }
            }
            return entries;
        }

        private static List<ModItemStoreState> BuildStates(List<StoreSaveEntry> entries)
        {
            Dictionary<string, ModItemStoreState> states = new Dictionary<string, ModItemStoreState>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                StoreSaveEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.StoreId))
                    continue;

                ModItemStoreState state;
                if (!states.TryGetValue(entry.StoreId, out state))
                {
                    state = new ModItemStoreState
                    {
                        OwnerId = entry.OwnerId,
                        StoreId = entry.StoreId,
                        DisplayName = entry.DisplayName,
                        Capacity = Math.Max(0, entry.Capacity)
                    };
                    states[entry.StoreId] = state;
                }

                if (!string.IsNullOrEmpty(entry.ItemId) && entry.Count > 0)
                    state.Items[entry.ItemId] = entry.Count;
            }

            return new List<ModItemStoreState>(states.Values);
        }

        private sealed class StoreSaveEntry
        {
            public string OwnerId = string.Empty;
            public string StoreId = string.Empty;
            public string DisplayName = string.Empty;
            public int Capacity;
            public string ItemId = string.Empty;
            public int Count;
        }
    }

    internal static class StoreItemMetadata
    {
        public static string DisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return string.Empty;

            ItemManager.ItemType type;
            if (ShelteredContent.Runtime.ResolveItemType(itemId, out type) && ItemManager.Instance != null)
            {
                GameItemDefinition definition = ItemManager.Instance.GetItemDefinition(type);
                if (definition != null && !string.IsNullOrEmpty(definition.NameLocalizationKey))
                {
                    try
                    {
                        string localized = Localization.Get(definition.NameLocalizationKey);
                        if (!string.IsNullOrEmpty(localized))
                            return localized;
                    }
                    catch
                    {
                    }
                }
            }

            return itemId;
        }

        public static ItemCategory Category(string itemId)
        {
            ItemManager.ItemType type;
            if (ShelteredContent.Runtime.ResolveItemType(itemId, out type) && ItemManager.Instance != null)
            {
                GameItemDefinition definition = ItemManager.Instance.GetItemDefinition(type);
                if (definition != null)
                    return (ItemCategory)(int)definition.Category;
            }

            if (InventoryItemStore.IsSpecialFood(itemId))
                return ItemCategory.Food;

            return ItemCategory.Normal;
        }
    }
}