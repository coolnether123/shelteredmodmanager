public static class ItemManager { public enum ItemType { Output, Wood, Metal } }
public sealed class CraftingManager {
    public sealed class Recipe {
        public struct Ingredient { public ItemManager.ItemType Item; public int Quantity; }
        public string ID; public int level; public bool unique, locked; public int location;
        public Ingredient[] Input;
        public Recipe(ItemManager.ItemType result, Ingredient[] input) { Input = input; }
    }
    public readonly List<Recipe> Added = new List<Recipe>();
    public bool AddRecipe(Recipe recipe) { Added.Add(recipe); return true; }
}
public sealed class IngredientDefinition { public string ItemId; public int Count; }
public sealed class RecipeDefinition {
    public string Id, ResultItemId, UnlockFlag; public int Station, Level; public bool Unique, Locked;
    public float CraftTimeSeconds = 1f; public List<IngredientDefinition> Ingredients = new List<IngredientDefinition>();
    public void NormalizeLegacyFields() { }
}
public static class ContentRegistry { public static readonly List<RecipeDefinition> Recipes = new List<RecipeDefinition>(); }
public static class MMLog {
    public static void WriteDebug(string message) { } public static void Write(string message) { }
    public static void WriteWarning(string message) { } public static void WriteError(string message) { }
    public static void WarnOnce(string key, string message) { }
}
public static class Safe { public static bool TryGetField<T>(object obj, string name, out T value) { value = default(T); return false; } }
public static partial class ContentRecipeContractFixture {
    private static readonly Dictionary<string, ItemManager.ItemType> ItemKeyToType = new Dictionary<string, ItemManager.ItemType>();
    private static bool TryParseEnum<T>(string text, out T value) where T : struct { return Enum.TryParse<T>(text, out value); }
    private static bool ResolveItemType(string text, out ItemManager.ItemType type) { return TryParseEnum(text, out type); }
    private static void ApplyLegacyResultCountCompatibility(RecipeDefinition def, ItemManager.ItemType result) { }
    private static int MapStation(int station) { return station; }
    private static bool IsValidCraftLocation(int location) { return true; }
    private static void ApplyRecipePatches() { }
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var def = new RecipeDefinition { Id = "fixture", ResultItemId = "Output" };
        def.Ingredients.Add(new IngredientDefinition { ItemId = "Wood", Count = 2 });
        def.Ingredients.Add(new IngredientDefinition { ItemId = "missing-mod-item", Count = 5 });
        ContentRegistry.Recipes.Add(def);
        var manager = new CraftingManager(); InjectRecipes(manager);
        Check(manager.Added.Count == 0, "A recipe with an unresolved ingredient must not be injected at a reduced price");
        def.Ingredients[1].ItemId = "Metal"; InjectRecipes(manager);
        Check(manager.Added.Count == 1 && manager.Added[0].Input.Length == 2, "Fully resolved recipes must still be injected");
        Check(manager.Added[0].Input[0].Quantity == 2 && manager.Added[0].Input[1].Quantity == 5, "Every declared ingredient quantity must be retained");
    }
}
