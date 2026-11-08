namespace ShelteredAPI.Content { public enum ItemCategory { Unknown } }
namespace ShelteredAPI.Storage {
    internal static class StoreItemMetadata {
        internal static string DisplayName(string id) { return id; }
        internal static ShelteredAPI.Content.ItemCategory Category(string id) { return ShelteredAPI.Content.ItemCategory.Unknown; }
    }
    internal static class ModItemStorePersistence { internal static void EnsureRegistered() { } }
}
public static class StorageContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var original = ShelteredAPI.Storage.ModItemStoreRegistry.Get("fixture", "store", "Store", 100);
        original.Add("item", 12);
        var reserved = ((ShelteredAPI.Storage.IReservableItemStore)original).Reserve("item", 2, "assignment.fixture");
        var loaded = new ShelteredAPI.Storage.ModItemStoreState { OwnerId = "fixture", StoreId = original.StoreId, DisplayName = "Loaded", Capacity = 50 };
        loaded.Items["item"] = 5;
        ShelteredAPI.Storage.ModItemStoreRegistry.ReplaceAll(new List<ShelteredAPI.Storage.ModItemStoreState> { loaded });
        var reopened = ShelteredAPI.Storage.ModItemStoreRegistry.Get("fixture", "store", null, -1);
        Check(original.GetCount("item") == 5 && reopened.GetCount("item") == 5, "Cached store handles must observe loaded contents");
        original.Add("item", 1);
        Check(reopened.GetCount("item") == 6, "Cached and reopened stores must keep sharing the loaded state");
        Check(!((ShelteredAPI.Storage.IReservableItemStore)original).CommitReservation(reserved.ReservationId).Success, "Loading must discard reservations from the previous save");
        ShelteredAPI.Storage.ModItemStoreRegistry.ResetContents();
        Check(original.GetCount("item") == 0 && reopened.GetCount("item") == 0, "New sessions must clear all existing store handles");
    }
}
