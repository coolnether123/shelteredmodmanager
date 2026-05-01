using System.Reflection;
using System.Collections.ObjectModel;
using UnityEngine;

namespace ShelteredAPI.Content
{
    /// <summary>
    /// Stable mod-facing content facade for registering Sheltered items, recipes, loot,
    /// localization, and mod-local assets.
    /// </summary>
    public static class ShelteredContent
    {
        private static readonly IShelteredContentService _service = new ShelteredContentService();

        public static IShelteredContentService Service
        {
            get { return _service; }
        }

        public static ReadOnlyCollection<ItemDefinitionSnapshot> RegisteredItems
        {
            get { return Service.Registration.GetRegisteredItems(); }
        }

        public static ReadOnlyCollection<RecipeDefinitionSnapshot> RegisteredRecipes
        {
            get { return Service.RecipeLootMutation.GetRegisteredRecipes(); }
        }

        public static RegistrationResult RegisterItem(ItemDefinition definition)
        {
            return Service.Registration.RegisterItem(definition);
        }

        public static RegistrationResult RegisterItem(string modId, string itemId, ItemDefinition definition)
        {
            return Service.Registration.RegisterItem(modId, itemId, definition);
        }

        public static ContentOperationResult RegisterRecipe(RecipeDefinition definition)
        {
            return Service.RecipeLootMutation.RegisterRecipe(definition);
        }

        public static ContentOperationResult RegisterCookingRecipe(CookingRecipe recipe)
        {
            return Service.RecipeLootMutation.RegisterCookingRecipe(recipe);
        }

        public static ContentOperationResult PatchItem(ItemPatch patch)
        {
            return Service.RecipeLootMutation.PatchItem(patch);
        }

        public static ContentOperationResult PatchRecipe(RecipePatch patch)
        {
            return Service.RecipeLootMutation.PatchRecipe(patch);
        }

        public static ContentOperationResult AddLoot(LootEntry entry)
        {
            return Service.RecipeLootMutation.AddLoot(entry);
        }

        public static ContentOperationResult SetLocalization(string key, string value)
        {
            return Service.Localization.Set(key, value);
        }

        public static bool TryGetLocalization(string key, out string value)
        {
            return Service.Localization.TryGet(key, out value);
        }

        [System.Obsolete("Use ShelteredContent.AdvancedAssets.LoadTexture(...) for Unity Texture2D access.")]
        public static Texture2D LoadTexture(Assembly assembly, string relativePath)
        {
            return AdvancedAssets.LoadTexture(assembly, relativePath);
        }

        [System.Obsolete("Use ShelteredContent.AdvancedAssets.LoadTexture(...) for Unity Texture2D access.")]
        public static Texture2D LoadTexture(string modRootPath, string relativePath)
        {
            return AdvancedAssets.LoadTexture(modRootPath, relativePath);
        }

        [System.Obsolete("Use ShelteredContent.AdvancedAssets.LoadSprite(...) for Unity Sprite access.")]
        public static Sprite LoadSprite(Assembly assembly, string relativePath)
        {
            return AdvancedAssets.LoadSprite(assembly, relativePath);
        }

        [System.Obsolete("Use ShelteredContent.AdvancedAssets.LoadSprite(...) for Unity Sprite access.")]
        public static Sprite LoadSprite(Assembly assembly, string relativePath, float pixelsPerUnit)
        {
            return AdvancedAssets.LoadSprite(assembly, relativePath, pixelsPerUnit);
        }

        [System.Obsolete("Use ShelteredContent.AdvancedAssets.LoadSprite(...) for Unity Sprite access.")]
        public static Sprite LoadSprite(string modRootPath, string relativePath)
        {
            return AdvancedAssets.LoadSprite(modRootPath, relativePath);
        }

        [System.Obsolete("Use ShelteredContent.AdvancedAssets.LoadSprite(...) for Unity Sprite access.")]
        public static Sprite LoadSprite(string modRootPath, string relativePath, float pixelsPerUnit)
        {
            return AdvancedAssets.LoadSprite(modRootPath, relativePath, pixelsPerUnit);
        }

        [System.Obsolete("Use ShelteredContent.AdvancedAssets.LoadBundle(...) for Unity AssetBundle access.")]
        public static AssetBundle LoadBundle(Assembly assembly, string relativePath)
        {
            return AdvancedAssets.LoadBundle(assembly, relativePath);
        }

        [System.Obsolete("Use ShelteredContent.AdvancedAssets.LoadBundle(...) for Unity AssetBundle access.")]
        public static AssetBundle LoadBundle(string modRootPath, string relativePath)
        {
            return AdvancedAssets.LoadBundle(modRootPath, relativePath);
        }

        [System.Obsolete("Use ShelteredContent.AdvancedAssets.LoadPrefabFromBundle(...) for Unity GameObject access.")]
        public static GameObject LoadPrefabFromBundle(AssetBundle bundle, string assetPath)
        {
            return AdvancedAssets.LoadPrefabFromBundle(bundle, assetPath);
        }

        [System.Obsolete("Use ShelteredContent.Runtime.ResolveItemType(...) for Sheltered runtime item type access.")]
        public static bool ResolveItemType(string itemId, out ItemManager.ItemType type)
        {
            return Runtime.ResolveItemType(itemId, out type);
        }

        [System.Obsolete("Use ShelteredContent.Runtime.TryGetCookingRecipe(...) for Sheltered runtime recipe access.")]
        public static bool TryGetCookingRecipe(ItemManager.ItemType rawItemType, out CookingRecipe recipe)
        {
            return Runtime.TryGetCookingRecipe(rawItemType, out recipe);
        }

        [System.Obsolete("Use ShelteredContent.Runtime.IsRawFood(...) for Sheltered runtime item type access.")]
        public static bool IsRawFood(ItemManager.ItemType itemType)
        {
            return Runtime.IsRawFood(itemType);
        }

        [System.Obsolete("Use ShelteredContent.Runtime.CreateItem(...) for Sheltered ItemInstance access.")]
        public static ItemInstance CreateItem(string itemId)
        {
            return Runtime.CreateItem(itemId);
        }

        [System.Obsolete("Use ShelteredContent.Runtime.TryAddToInventory(...) for Sheltered ItemInstance access.")]
        public static bool TryAddToInventory(ItemInstance item)
        {
            return Runtime.TryAddToInventory(item);
        }

        public static bool TryAddToInventory(string itemId, int quantity)
        {
            return Service.Inventory.TryAdd(itemId, quantity).Success;
        }

        public static InventoryMutationResult AddToInventory(string itemId, int quantity)
        {
            return Service.Inventory.TryAdd(itemId, quantity);
        }

        public static bool TryRemoveFromInventory(string itemId, int quantity)
        {
            return Service.Inventory.TryRemove(itemId, quantity).Success;
        }

        public static InventoryMutationResult RemoveFromInventory(string itemId, int quantity)
        {
            return Service.Inventory.TryRemove(itemId, quantity);
        }

        public static int GetItemCount(string itemId)
        {
            return Service.Inventory.GetCount(itemId);
        }

        public static int GetItemCount(string itemId, bool includeParties)
        {
            return Service.Inventory.GetCount(itemId, includeParties);
        }

        public static ReadOnlyCollection<ItemStack> GetAllInventoryItems()
        {
            return Service.Inventory.GetAllItems();
        }

        public static int GetStorageCapacity()
        {
            return Service.Inventory.GetStorageCapacity();
        }

        public static int GetUsedStorage()
        {
            return Service.Inventory.GetUsedStorage();
        }

        public static class AdvancedAssets
        {
            public static Texture2D LoadTexture(Assembly assembly, string relativePath)
            {
                return Service.Assets.LoadTexture(assembly, relativePath);
            }

            public static Texture2D LoadTexture(string modRootPath, string relativePath)
            {
                return Service.Assets.LoadTexture(modRootPath, relativePath);
            }

            public static Sprite LoadSprite(Assembly assembly, string relativePath)
            {
                return Service.Assets.LoadSprite(assembly, relativePath);
            }

            public static Sprite LoadSprite(Assembly assembly, string relativePath, float pixelsPerUnit)
            {
                return Service.Assets.LoadSprite(assembly, relativePath, pixelsPerUnit);
            }

            public static Sprite LoadSprite(string modRootPath, string relativePath)
            {
                return Service.Assets.LoadSprite(modRootPath, relativePath);
            }

            public static Sprite LoadSprite(string modRootPath, string relativePath, float pixelsPerUnit)
            {
                return Service.Assets.LoadSprite(modRootPath, relativePath, pixelsPerUnit);
            }

            public static AssetBundle LoadBundle(Assembly assembly, string relativePath)
            {
                return Service.Assets.LoadBundle(assembly, relativePath);
            }

            public static AssetBundle LoadBundle(string modRootPath, string relativePath)
            {
                return Service.Assets.LoadBundle(modRootPath, relativePath);
            }

            public static GameObject LoadPrefabFromBundle(AssetBundle bundle, string assetPath)
            {
                return Service.Assets.LoadPrefabFromBundle(bundle, assetPath);
            }
        }

        public static class Runtime
        {
            public static bool ResolveItemType(string itemId, out ItemManager.ItemType type)
            {
                return InventoryHelper.ResolveItemType(itemId, out type);
            }

            public static bool TryGetCookingRecipe(ItemManager.ItemType rawItemType, out CookingRecipe recipe)
            {
                return ContentInjector.TryGetCookingRecipe(rawItemType, out recipe);
            }

            public static bool IsRawFood(ItemManager.ItemType itemType)
            {
                return ContentInjector.IsRawFood(itemType);
            }

            public static ItemInstance CreateItem(string itemId)
            {
                return InventoryHelper.CreateItem(itemId);
            }

            public static bool TryAddToInventory(ItemInstance item)
            {
                return InventoryHelper.TryAddToInventory(item);
            }
        }
    }
}
