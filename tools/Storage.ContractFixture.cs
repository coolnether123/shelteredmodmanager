namespace ShelteredAPI.Content { public enum ItemCategory { Unknown } }
namespace ShelteredAPI.Storage {
    internal static class StoreItemMetadata {
        internal static string DisplayName(string id) { return id; }
        internal static ShelteredAPI.Content.ItemCategory Category(string id) { return ShelteredAPI.Content.ItemCategory.Unknown; }
    }
    internal static class ModItemStorePersistence { internal static void EnsureRegistered() { } }
    internal static class CharacterItemAssignmentPersistence { internal static void EnsureRegistered() { } }
    internal static partial class ShelteredStores {
        internal static bool TryResolveStore(string id, ItemStoreKind kind, out IItemStore store) { return ModItemStoreRegistry.TryGet(id, out store); }
        private static string GetCannotAddMessage(IItemStore store, string id) { return "Cannot add"; }
    }
}
public class FoodManager {
    public static FoodManager Instance = new FoodManager();
    public int Rations = 19;
    public bool AddRations(int quantity) { Rations += quantity; if (Rations > 20) { Rations = 20; return false; } return true; }
    public int TakeRations(int quantity) { int before = Rations; Rations = Math.Max(0, Rations - quantity); return before - Rations; }
    public int AddMeat(int quantity) { return quantity; }
    public int AddDesperateMeat(int quantity) { return quantity; }
}
namespace ShelteredAPI.Content {
    internal class InventoryMutationResult { public bool Success = true; public string ErrorMessage = ""; }
    internal static class ShelteredContent {
        internal static int Stored = 19;
        public static InventoryMutationResult AddToInventory(string id, int quantity) { int before = Stored; Stored = Math.Min(20, Stored + quantity); return new InventoryMutationResult { Success = Stored - before == quantity }; }
    }
}
namespace ShelteredAPI.Storage {
    internal partial class InventoryItemStore : ItemStoreBase {
        public override string StoreId { get { return "inventory"; } }
        public override string DisplayName { get { return "Inventory"; } }
        public override ItemStoreKind Kind { get { return ItemStoreKind.Inventory; } }
        public override int Capacity { get { return 20; } }
        public override int Used { get { return FoodManager.Instance.Rations; } }
        public override ItemStoreSnapshot Snapshot() { return new ItemStoreSnapshot(); }
        public override int GetCount(string id) { return IsRation(id) ? FoodManager.Instance.Rations : ShelteredAPI.Content.ShelteredContent.Stored; }
        public override bool CanAdd(string id, int quantity) { return true; }
        public override bool CanRemove(string id, int quantity) { return GetCount(id) >= quantity; }
        public override ItemTransferResult Remove(string id, int quantity) { if (IsRation(id)) return ItemTransferResult.Ok(id, quantity, FoodManager.Instance.TakeRations(quantity)); ShelteredAPI.Content.ShelteredContent.Stored -= quantity; return ItemTransferResult.Ok(id, quantity, quantity); }
        private static bool IsRation(string id) { return id == "Ration"; }
        private static bool IsMeat(string id) { return false; }
        private static bool IsDesperateMeat(string id) { return false; }
    }
}
public class FamilyMember {
    public string firstName, lastName;
    public int GetId() { return 1; }
}
namespace ShelteredAPI.Actors {
    internal static class ShelteredActors {
        internal static readonly FixtureActors Instance = new FixtureActors();
        internal static ModAPI.Actors.ActorId FamilyMemberActorId(int id) { return new ModAPI.Actors.ActorId(ModAPI.Actors.ActorKind.Player, id, ""); }
    }
    internal class FixtureActors {
        internal bool TryGet(ModAPI.Actors.ActorId id, out ModAPI.Actors.IActorRecord record) { record = null; return true; }
        internal void Ensure(ModAPI.Actors.ActorCreateRequest request) { }
    }
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
        original.Add("item", 5);
        var assignments = ShelteredAPI.Storage.CharacterItemAssignmentService.Instance;
        assignments.Assign(new FamilyMember(), original, "item", 1, ShelteredAPI.Storage.CharacterItemAssignmentKind.Assigned, ShelteredAPI.Storage.CharacterItemSlot.None);
        Check(assignments.Snapshot().Count == 1, "Assignment fixture must create an assignment");
        assignments.ResetForSession();
        Check(assignments.Snapshot().Count == 0, "A new session must discard the previous character assignments");
        var sources = (System.Collections.IDictionary)assignments.GetType().GetField("_knownSources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(assignments);
        Check(sources.Count == 0, "Loaded assignments must resolve stores from the current scene");
        var inventory = new ShelteredAPI.Storage.InventoryItemStore();
        Check(inventory.Add("Ration", 5).Moved == 1, "Ration adds must report partial pantry acceptance");
        FoodManager.Instance.Rations = 19;
        original.Add("Ration", 5);
        var transfer = ShelteredAPI.Storage.ShelteredStores.Transfer(original, inventory, "Ration", 5);
        Check(!transfer.Success && original.GetCount("Ration") == 5 && FoodManager.Instance.Rations == 19, "A rejected partial ration transfer must restore both stores without duplicating items");
        original.Add("Nail", 5);
        transfer = ShelteredAPI.Storage.ShelteredStores.Transfer(original, inventory, "Nail", 5);
        Check(!transfer.Success && original.GetCount("Nail") == 5 && ShelteredAPI.Content.ShelteredContent.Stored == 19, "A partial ordinary-item transfer must roll back both stores");
    }
}
