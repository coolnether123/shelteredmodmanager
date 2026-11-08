public sealed class ScenarioDefinition { }
public sealed class ScenarioRuntimeState { }
public enum ScenarioEffectKind { AddInventory, RemoveInventory }
public enum ScenarioConditionKind { ItemQuantityAvailable }
public sealed class ScenarioEffectDefinition { public ScenarioEffectKind Kind; public string ItemId; public int Quantity; }
public sealed class ScenarioConditionRef { public string TargetId; public int Quantity; }
public static class ItemManager { public enum ItemType { Wood, Ration, Meat, DesperateMeat } }
public sealed class InventoryManager {
    public static readonly InventoryManager Instance = new InventoryManager();
    public int Wood;
    public int GetItemCountInStorage(ItemManager.ItemType type, bool parties) { return type == ItemManager.ItemType.Wood ? Wood : 0; }
    public bool RemoveItemsOfType(ItemManager.ItemType type, int quantity) { int before = Wood; Wood = Math.Max(0, Wood - quantity); return before >= quantity; }
    public bool AddNewItems(ItemManager.ItemType type, int quantity) { Wood += quantity; return true; }
}
public static class InventoryHelper {
    public static bool ResolveItemType(string id, out ItemManager.ItemType type) { return Enum.TryParse<ItemManager.ItemType>(id, out type); }
}
public sealed class ItemTransferResult { public bool Success; public int Moved; public string ErrorMessage = null; }
public static class ShelteredStores {
    public static readonly FoodAwareStore Inventory = new FoodAwareStore();
    public static FoodAwareStore ForInventory() { return Inventory; }
}
public sealed class FoodAwareStore {
    public readonly Dictionary<string, int> Food = new Dictionary<string, int>();
    public int GetCount(string id) { int count; return Food.TryGetValue(id, out count) ? count : InventoryManager.Instance.Wood; }
    public ItemTransferResult Remove(string id, int quantity) {
        if (GetCount(id) < quantity) return new ItemTransferResult { ErrorMessage = "Not enough items" };
        if (Food.ContainsKey(id)) Food[id] -= quantity; else InventoryManager.Instance.Wood -= quantity;
        return new ItemTransferResult { Success = true, Moved = quantity };
    }
}
public partial class ScenarioInventoryContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var service = new ScenarioInventoryContractFixture();
        string reason;
        foreach (string food in new[] { "Ration", "Meat", "DesperateMeat" }) {
            ShelteredStores.Inventory.Food[food] = 4;
            Check(service.IsSatisfied(null, new ScenarioConditionRef { TargetId = food, Quantity = 4 }, null, out reason), "Inventory gates must count " + food + " outside vanilla's ordinary dictionary");
            Check(service.Handle(null, new ScenarioEffectDefinition { Kind = ScenarioEffectKind.RemoveInventory, ItemId = food, Quantity = 3 }, null, out reason) && ShelteredStores.Inventory.Food[food] == 1, "Inventory effects must remove " + food + " through its food manager");
        }
        InventoryManager.Instance.Wood = 2;
        Check(!service.Handle(null, new ScenarioEffectDefinition { Kind = ScenarioEffectKind.RemoveInventory, ItemId = "Wood", Quantity = 3 }, null, out reason) && InventoryManager.Instance.Wood == 2, "A failed removal effect must preserve the existing ordinary items");
        Check(service.Handle(null, new ScenarioEffectDefinition { Kind = ScenarioEffectKind.RemoveInventory, ItemId = "Wood", Quantity = 2 }, null, out reason) && InventoryManager.Instance.Wood == 0, "A complete ordinary-item removal must still succeed");
    }
}
