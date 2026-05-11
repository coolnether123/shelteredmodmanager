using System.Collections.ObjectModel;
using System.Reflection;
using UnityEngine;

using ShelteredAPI.Content.Compatibility;
namespace ShelteredAPI.Content
{
    /// <summary>
    /// Combined service-oriented entry point behind the ShelteredContent facade.
    /// </summary>
    public interface IShelteredContentService
    {
        IShelteredContentRegistrationService Registration { get; }
        IShelteredInventoryService Inventory { get; }
        IShelteredAssetLoadingService Assets { get; }
        IShelteredLocalizationService Localization { get; }
        IShelteredRecipeLootMutationService RecipeLootMutation { get; }
    }

    /// <summary>Registers new authoring definitions and exposes read-only catalog snapshots.</summary>
    public interface IShelteredContentRegistrationService
    {
        RegistrationResult RegisterItem(ItemDefinition definition);
        RegistrationResult RegisterItem(string modId, string itemId, ItemDefinition definition);
        ReadOnlyCollection<ItemDefinitionSnapshot> GetRegisteredItems();
    }

    /// <summary>Inventory operations that prefer mod-facing IDs over Sheltered runtime types.</summary>
    public interface IShelteredInventoryService
    {
        InventoryMutationResult TryAdd(string itemId, int quantity);
        InventoryMutationResult TryRemove(string itemId, int quantity);
        int GetCount(string itemId);
        int GetCount(string itemId, bool includeParties);
        ReadOnlyCollection<ItemStack> GetAllItems();
        int GetStorageCapacity();
        int GetUsedStorage();
    }

    /// <summary>Advanced Unity asset loading for mods that intentionally need Unity objects.</summary>
    public interface IShelteredAssetLoadingService
    {
        Texture2D LoadTexture(Assembly assembly, string relativePath);
        Texture2D LoadTexture(string modRootPath, string relativePath);
        Sprite LoadSprite(Assembly assembly, string relativePath);
        Sprite LoadSprite(Assembly assembly, string relativePath, float pixelsPerUnit);
        Sprite LoadSprite(string modRootPath, string relativePath);
        Sprite LoadSprite(string modRootPath, string relativePath, float pixelsPerUnit);
        AssetBundle LoadBundle(Assembly assembly, string relativePath);
        AssetBundle LoadBundle(string modRootPath, string relativePath);
        GameObject LoadPrefabFromBundle(AssetBundle bundle, string assetPath);
    }

    /// <summary>Localization key/value access for ShelteredAPI content.</summary>
    public interface IShelteredLocalizationService
    {
        ContentOperationResult Set(string key, string value);
        bool TryGet(string key, out string value);
    }

    /// <summary>Recipe and loot table mutation API separated from item registration.</summary>
    public interface IShelteredRecipeLootMutationService
    {
        ContentOperationResult RegisterRecipe(RecipeDefinition definition);
        ContentOperationResult RegisterCookingRecipe(CookingRecipe recipe);
        ContentOperationResult PatchItem(ItemPatch patch);
        ContentOperationResult PatchRecipe(RecipePatch patch);
        ContentOperationResult AddLoot(LootEntry entry);
        ReadOnlyCollection<RecipeDefinitionSnapshot> GetRegisteredRecipes();
    }

    internal sealed class ShelteredContentService : IShelteredContentService
    {
        private readonly IShelteredContentRegistrationService _registration;
        private readonly IShelteredInventoryService _inventory;
        private readonly IShelteredAssetLoadingService _assets;
        private readonly IShelteredLocalizationService _localization;
        private readonly IShelteredRecipeLootMutationService _recipeLootMutation;

        public ShelteredContentService()
        {
            _registration = new ShelteredContentRegistrationService();
            _inventory = new ShelteredInventoryService();
            _assets = new ShelteredAssetLoadingService();
            _localization = new ShelteredLocalizationService();
            _recipeLootMutation = new ShelteredRecipeLootMutationService();
        }

        public IShelteredContentRegistrationService Registration { get { return _registration; } }
        public IShelteredInventoryService Inventory { get { return _inventory; } }
        public IShelteredAssetLoadingService Assets { get { return _assets; } }
        public IShelteredLocalizationService Localization { get { return _localization; } }
        public IShelteredRecipeLootMutationService RecipeLootMutation { get { return _recipeLootMutation; } }
    }

    internal sealed class ShelteredContentRegistrationService : IShelteredContentRegistrationService
    {
        public RegistrationResult RegisterItem(ItemDefinition definition)
        {
            ContentOwnerAssemblyResolver.EnsureOwner(definition);
            return ContentRegistry.RegisterItem(definition);
        }

        public RegistrationResult RegisterItem(string modId, string itemId, ItemDefinition definition)
        {
            ContentOwnerAssemblyResolver.EnsureOwner(definition);
            return ContentRegistry.RegisterItemWithFixedId(modId, itemId, definition);
        }

        public ReadOnlyCollection<ItemDefinitionSnapshot> GetRegisteredItems()
        {
            return ContentRegistry.GetItemSnapshots();
        }
    }

    internal sealed class ShelteredInventoryService : IShelteredInventoryService
    {
        public InventoryMutationResult TryAdd(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId))
                return InventoryMutationResult.Failed(itemId, quantity, "Item ID is required");
            if (quantity <= 0)
                return InventoryMutationResult.Failed(itemId, quantity, "Quantity must be greater than zero");

            bool success = InventoryHelper.TryAddToInventory(itemId, quantity);
            return success
                ? InventoryMutationResult.Ok(itemId, quantity)
                : InventoryMutationResult.Failed(itemId, quantity, "Inventory rejected the item or runtime item ID could not be resolved");
        }

        public InventoryMutationResult TryRemove(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId))
                return InventoryMutationResult.Failed(itemId, quantity, "Item ID is required");
            if (quantity <= 0)
                return InventoryMutationResult.Failed(itemId, quantity, "Quantity must be greater than zero");

            bool success = InventoryHelper.TryRemoveFromInventory(itemId, quantity);
            return success
                ? InventoryMutationResult.Ok(itemId, quantity)
                : InventoryMutationResult.Failed(itemId, quantity, "Inventory did not contain enough items or runtime item ID could not be resolved");
        }

        public int GetCount(string itemId)
        {
            return InventoryHelper.GetItemCount(itemId);
        }

        public int GetCount(string itemId, bool includeParties)
        {
            return InventoryHelper.GetItemCount(itemId, includeParties);
        }

        public ReadOnlyCollection<ItemStack> GetAllItems()
        {
            return InventoryHelper.GetAllItems();
        }

        public int GetStorageCapacity()
        {
            return InventoryHelper.GetStorageCapacity();
        }

        public int GetUsedStorage()
        {
            return InventoryHelper.GetUsedStorage();
        }
    }

    internal sealed class ShelteredAssetLoadingService : IShelteredAssetLoadingService
    {
        public Texture2D LoadTexture(Assembly assembly, string relativePath)
        {
            return AssetLoader.LoadTexture(assembly, relativePath);
        }

        public Texture2D LoadTexture(string modRootPath, string relativePath)
        {
            return AssetLoader.LoadTexture(modRootPath, relativePath);
        }

        public Sprite LoadSprite(Assembly assembly, string relativePath)
        {
            return AssetLoader.LoadSprite(assembly, relativePath);
        }

        public Sprite LoadSprite(Assembly assembly, string relativePath, float pixelsPerUnit)
        {
            return AssetLoader.LoadSprite(assembly, relativePath, pixelsPerUnit);
        }

        public Sprite LoadSprite(string modRootPath, string relativePath)
        {
            return AssetLoader.LoadSprite(modRootPath, relativePath);
        }

        public Sprite LoadSprite(string modRootPath, string relativePath, float pixelsPerUnit)
        {
            return AssetLoader.LoadSprite(modRootPath, relativePath, pixelsPerUnit);
        }

        public AssetBundle LoadBundle(Assembly assembly, string relativePath)
        {
            return AssetLoader.LoadBundle(assembly, relativePath);
        }

        public AssetBundle LoadBundle(string modRootPath, string relativePath)
        {
            return AssetLoader.LoadBundle(modRootPath, relativePath);
        }

        public GameObject LoadPrefabFromBundle(AssetBundle bundle, string assetPath)
        {
            return AssetLoader.LoadPrefabFromBundle(bundle, assetPath);
        }
    }

    internal sealed class ShelteredLocalizationService : IShelteredLocalizationService
    {
        public ContentOperationResult Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return ContentOperationResult.Failed("Localization key is required");

            ModLocalization.Set(key, value);
            return ContentOperationResult.Ok();
        }

        public bool TryGet(string key, out string value)
        {
            return ModLocalization.TryGet(key, out value);
        }
    }

    internal sealed class ShelteredRecipeLootMutationService : IShelteredRecipeLootMutationService
    {
        public ContentOperationResult RegisterRecipe(RecipeDefinition definition)
        {
            return ContentRegistry.RegisterRecipe(definition);
        }

        public ContentOperationResult RegisterCookingRecipe(CookingRecipe recipe)
        {
            return ContentRegistry.RegisterCookingRecipe(recipe);
        }

        public ContentOperationResult PatchItem(ItemPatch patch)
        {
            return ContentRegistry.PatchItem(patch);
        }

        public ContentOperationResult PatchRecipe(RecipePatch patch)
        {
            return ContentRegistry.PatchRecipe(patch);
        }

        public ContentOperationResult AddLoot(LootEntry entry)
        {
            return ContentRegistry.AddLoot(entry);
        }

        public ReadOnlyCollection<RecipeDefinitionSnapshot> GetRegisteredRecipes()
        {
            return ContentRegistry.GetRecipeSnapshots();
        }
    }
}
