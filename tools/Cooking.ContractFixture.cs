public class CookingStationRecipe { public string OutputItemId = "food"; public int OutputCount = 1; }
public class CraftingUiRecipe { public CookingStationRecipe Recipe = new CookingStationRecipe(); }
public class CraftingUiCraftContext { public CraftingUiRecipe Recipe; public Panel Panel; }
public class CookingStationRequest {
    public Func<CookingStationRecipe, string> GetUnavailableReason;
    public Action<CookingCraftContext> OnCrafted;
}
public class Panel { public void Refresh() { } }
public class CookingCraftContext {
    public Panel Panel;
    public ItemTransferResult Result;
    public CookingCraftContext(CookingStationRecipe recipe, IItemStore source, IItemStore target, Panel panel) { Panel = panel; }
}
public interface IItemStore { bool CanAdd(string id, int count); }
public class ItemTransferResult {
    public bool Success;
    public string ErrorMessage;
    public static ItemTransferResult Failed(string id, int count, string message) { return new ItemTransferResult { ErrorMessage = message }; }
}
public static partial class CookingContractFixture {
    public static int Count;
    private static int applied;
    private static CookingCraftContext failed;
    private static CookingStationRecipe ResolveRecipe(CraftingUiRecipe recipe) { return recipe == null ? null : recipe.Recipe; }
    private static bool HasIngredients(CookingStationRecipe recipe, IItemStore store) { return store != null; }
    private static bool ShouldQueueJob(CookingStationRecipe recipe, CookingStationRequest request) { return false; }
    private static void QueueCraftJob(CookingStationRecipe recipe, CookingStationRequest request, IItemStore source, IItemStore target, CookingCraftContext context) { applied++; }
    private static ItemTransferResult ApplyRecipe(CookingStationRecipe recipe, CookingStationRequest request, IItemStore source, IItemStore target) { applied++; return new ItemTransferResult { Success = true }; }
    private static void NotifyFailed(CookingStationRequest request, CookingCraftContext context) { failed = context; }
    private sealed class Store : IItemStore { public bool CanAdd(string id, int count) { return true; } }
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var store = new Store();
        var recipe = new CraftingUiRecipe();
        string reason = "Station locked";
        var request = new CookingStationRequest { GetUnavailableReason = delegate { return reason; } };
        Check(!IsAvailable(recipe, store, store, request), "A custom unavailable reason must disable cooking");
        ExecuteCraft(new CraftingUiCraftContext { Recipe = recipe }, request, store, store);
        Check(applied == 0 && failed != null && failed.Result.ErrorMessage == reason, "Blocked cooking must report the reason without applying or queuing a recipe");
        reason = null;
        Check(IsAvailable(recipe, store, store, request), "Clearing the custom reason must enable cooking");
        ExecuteCraft(new CraftingUiCraftContext { Recipe = recipe }, request, store, store);
        Check(applied == 1, "Allowed cooking must apply the recipe");
    }
}
