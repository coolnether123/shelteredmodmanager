public class ItemManager {
    public enum ItemType { Nail, Ration, Meat, DesperateMeat, Water, Book, Toy, Record }
}
public class InventoryManager {
    public static InventoryManager Instance = new InventoryManager();
    public int Count = 2;
    public int GetItemCountInStorage(ItemManager.ItemType type, bool parties) { return type == ItemManager.ItemType.Nail ? Count : 0; }
    public bool RemoveItemsOfType(ItemManager.ItemType type, int requested) { int moved = Math.Min(Count, requested); Count -= moved; return moved == requested; }
}
public class FoodManager { public static FoodManager Instance = new FoodManager(); public int Rations = 3, Meat = 4, DesperateMeat = 5; }
public class WaterManager { public static WaterManager Instance = new WaterManager(); public float StoredWater = 6; }
public class EntertainmentManager { public static EntertainmentManager Instance = new EntertainmentManager(); public int Books = 7, Toys = 8, Records = 9; }
public static partial class InventoryContractFixture {
    private static InventoryManager Mgr { get { return InventoryManager.Instance; } }
    public static bool ResolveItemType(string id, out ItemManager.ItemType type) { type = ItemManager.ItemType.Nail; return id == "Nail"; }
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void VerifyRemoval() {
        Check(!TryRemoveFromInventory("Nail", 3) && Mgr.Count == 2, "An insufficient removal must preserve all remaining items");
        Check(TryRemoveFromInventory("Nail", 2) && Mgr.Count == 0, "A sufficient removal must remove the requested items");
    }
    public static void VerifyCounts() {
        Check(GetItemCountStrict(ItemManager.ItemType.DesperateMeat) == 5, "Total owned counts must include desperate meat in freezers");
    }
}
