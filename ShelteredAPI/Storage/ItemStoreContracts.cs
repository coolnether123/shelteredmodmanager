using System.Collections.Generic;
using ShelteredAPI.Content;

namespace ShelteredAPI.Storage
{
    /// <summary>
    /// Broad runtime kind for item stores exposed through ShelteredAPI.
    /// </summary>
    public enum ItemStoreKind
    {
        Unknown,
        Inventory,
        Freezer,
        Mod
    }

    /// <summary>
    /// Stable item row used by store snapshots and runtime UI adapters.
    /// </summary>
    public sealed class ItemStoreItem
    {
        public ItemStoreItem()
        {
        }

        public ItemStoreItem(string itemId, string displayName, ItemCategory category, int count)
        {
            ItemId = itemId;
            DisplayName = displayName;
            Category = category;
            Count = count;
        }

        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public string Subtitle { get; set; }
        public ItemCategory Category { get; set; }
        public int Count { get; set; }
        public object Tag { get; set; }
    }

    /// <summary>
    /// Point-in-time view of an item store.
    /// </summary>
    public sealed class ItemStoreSnapshot
    {
        public ItemStoreSnapshot()
        {
            Items = new List<ItemStoreItem>();
        }

        public string StoreId { get; set; }
        public string DisplayName { get; set; }
        public ItemStoreKind Kind { get; set; }
        public int Capacity { get; set; }
        public int Used { get; set; }
        public bool IsReadOnly { get; set; }
        public IList<ItemStoreItem> Items { get; set; }
    }

    /// <summary>
    /// Result from adding, removing, or transferring store contents.
    /// </summary>
    public sealed class ItemTransferResult
    {
        public bool Success { get; private set; }
        public string ItemId { get; private set; }
        public int Requested { get; private set; }
        public int Moved { get; private set; }
        public string ErrorMessage { get; private set; }

        public static ItemTransferResult Ok(string itemId, int requested, int moved)
        {
            return new ItemTransferResult
            {
                Success = true,
                ItemId = itemId,
                Requested = requested,
                Moved = moved
            };
        }

        public static ItemTransferResult Failed(string itemId, int requested, string error)
        {
            return new ItemTransferResult
            {
                Success = false,
                ItemId = itemId,
                Requested = requested,
                Moved = 0,
                ErrorMessage = error
            };
        }
    }

    /// <summary>
    /// Result from reserving store contents for a later commit or cancellation.
    /// </summary>
    public sealed class ItemReservationResult
    {
        public bool Success { get; private set; }
        public string ReservationId { get; private set; }
        public string ItemId { get; private set; }
        public int Requested { get; private set; }
        public int Reserved { get; private set; }
        public string OwnerToken { get; private set; }
        public string ErrorMessage { get; private set; }

        public static ItemReservationResult Ok(string reservationId, string itemId, int requested, int reserved, string ownerToken)
        {
            return new ItemReservationResult
            {
                Success = true,
                ReservationId = reservationId,
                ItemId = itemId,
                Requested = requested,
                Reserved = reserved,
                OwnerToken = ownerToken
            };
        }

        public static ItemReservationResult Failed(string itemId, int requested, string ownerToken, string error)
        {
            return new ItemReservationResult
            {
                Success = false,
                ItemId = itemId,
                Requested = requested,
                Reserved = 0,
                OwnerToken = ownerToken,
                ErrorMessage = error
            };
        }
    }

    /// <summary>
    /// Minimal store interface for moving mod-facing item IDs without depending on vanilla manager internals.
    /// </summary>
    public interface IItemStore
    {
        string StoreId { get; }
        string DisplayName { get; }
        ItemStoreKind Kind { get; }
        int Capacity { get; }
        int Used { get; }
        bool IsReadOnly { get; }

        ItemStoreSnapshot Snapshot();
        int GetCount(string itemId);
        bool CanAdd(string itemId, int quantity);
        bool CanRemove(string itemId, int quantity);
        ItemTransferResult Add(string itemId, int quantity);
        ItemTransferResult Remove(string itemId, int quantity);
    }

    /// <summary>
    /// Optional extension for stores that can hold items for a queued job before final consumption.
    /// </summary>
    public interface IReservableItemStore
    {
        ItemReservationResult Reserve(string itemId, int quantity, string ownerToken);
        ItemTransferResult CommitReservation(string reservationId);
        ItemTransferResult CancelReservation(string reservationId);
        int GetAvailableCount(string itemId);
    }
}
