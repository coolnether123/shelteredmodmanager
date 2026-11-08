namespace ShelteredAPI.Content { public enum ItemCategory { Unknown } }
namespace ShelteredAPI.Storage {
    internal static class StoreItemMetadata {
        internal static string DisplayName(string id) { return id; }
        internal static ShelteredAPI.Content.ItemCategory Category(string id) { return ShelteredAPI.Content.ItemCategory.Unknown; }
    }
    internal static class ModItemStorePersistence { internal static void EnsureRegistered() { } }
    internal static class CharacterItemAssignmentPersistence { internal static void EnsureRegistered() { } }
    internal static class ShelteredStores {
        internal static bool TryResolveStore(string id, ItemStoreKind kind, out IItemStore store) { return ModItemStoreRegistry.TryGet(id, out store); }
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
    }
}
