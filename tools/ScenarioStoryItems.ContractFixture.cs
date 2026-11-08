public static class ItemManager { public enum ItemType { Undefined, Wood } }
public sealed class ItemEntry { public string ItemId; public int Quantity; }
public sealed class ItemStack {
    public ItemManager.ItemType Type;
    public int Quantity;
    public ItemStack(ItemManager.ItemType type, int quantity) { Type = type; Quantity = quantity; }
}
public static class ContentInjector {
    public static bool ResolveItemType(string id, out ItemManager.ItemType type) {
        if (id == "fixture.custom_item") { type = (ItemManager.ItemType)1000; return true; }
        return Enum.TryParse<ItemManager.ItemType>(id, true, out type);
    }
}
public static partial class ScenarioStoryItemsContractFixture {
    public static int Count;
    private static IList GetRequiredList(object target, FieldInfo field, string name) { return (IList)target; }
    public static void Verify() {
        var items = new List<ItemStack>();
        ReplaceItemList(items, null, new List<ItemEntry> {
            new ItemEntry { ItemId = "fixture.custom_item", Quantity = 3 },
            new ItemEntry { ItemId = "Wood", Quantity = 2 },
            new ItemEntry { ItemId = "unknown", Quantity = 1 }
        }, "fixture");
        Count++; if (items.Count != 2) throw new Exception("Story item lists must retain registered custom items and skip unknown IDs");
        Count++; if ((int)items[0].Type != 1000 || items[0].Quantity != 3) throw new Exception("Custom story items must use their registered runtime type and quantity");
        Count++; if (items[1].Type != ItemManager.ItemType.Wood || items[1].Quantity != 2) throw new Exception("Vanilla story item mapping must remain intact");
    }
}
