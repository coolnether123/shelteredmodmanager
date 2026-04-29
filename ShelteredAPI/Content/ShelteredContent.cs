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
        public static RegistrationResult RegisterItem(ItemDefinition definition)
        {
            return ContentRegistry.RegisterItem(definition);
        }

        public static RegistrationResult RegisterItem(string modId, string itemId, ItemDefinition definition)
        {
            return ContentRegistry.RegisterItemWithFixedId(modId, itemId, definition);
        }

        public static void RegisterRecipe(RecipeDefinition definition)
        {
            ContentRegistry.RegisterRecipe(definition);
        }

        public static void RegisterCookingRecipe(CookingRecipe recipe)
        {
            ContentRegistry.RegisterCookingRecipe(recipe);
        }

        public static void PatchItem(ItemPatch patch)
        {
            ContentRegistry.PatchItem(patch);
        }

        public static void PatchRecipe(RecipePatch patch)
        {
            ContentRegistry.PatchRecipe(patch);
        }

        public static void AddLoot(LootEntry entry)
        {
            ContentRegistry.AddLoot(entry);
        }

        public static void SetLocalization(string key, string value)
        {
            ModLocalization.Set(key, value);
        }

        public static bool TryGetLocalization(string key, out string value)
        {
            return ModLocalization.TryGet(key, out value);
        }

        public static Texture2D LoadTexture(Assembly assembly, string relativePath)
        {
            return AssetLoader.LoadTexture(assembly, relativePath);
        }

        public static Texture2D LoadTexture(string modRootPath, string relativePath)
        {
            return AssetLoader.LoadTexture(modRootPath, relativePath);
        }

        public static Sprite LoadSprite(Assembly assembly, string relativePath)
        {
            return AssetLoader.LoadSprite(assembly, relativePath);
        }

        public static Sprite LoadSprite(Assembly assembly, string relativePath, float pixelsPerUnit)
        {
            return AssetLoader.LoadSprite(assembly, relativePath, pixelsPerUnit);
        }

        public static Sprite LoadSprite(string modRootPath, string relativePath)
        {
            return AssetLoader.LoadSprite(modRootPath, relativePath);
        }

        public static Sprite LoadSprite(string modRootPath, string relativePath, float pixelsPerUnit)
        {
            return AssetLoader.LoadSprite(modRootPath, relativePath, pixelsPerUnit);
        }

        public static AssetBundle LoadBundle(Assembly assembly, string relativePath)
        {
            return AssetLoader.LoadBundle(assembly, relativePath);
        }

        public static AssetBundle LoadBundle(string modRootPath, string relativePath)
        {
            return AssetLoader.LoadBundle(modRootPath, relativePath);
        }

        public static GameObject LoadPrefabFromBundle(AssetBundle bundle, string assetPath)
        {
            return AssetLoader.LoadPrefabFromBundle(bundle, assetPath);
        }

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

        public static bool TryAddToInventory(string itemId, int quantity)
        {
            return InventoryHelper.TryAddToInventory(itemId, quantity);
        }

        public static bool TryRemoveFromInventory(string itemId, int quantity)
        {
            return InventoryHelper.TryRemoveFromInventory(itemId, quantity);
        }

        public static int GetItemCount(string itemId)
        {
            return InventoryHelper.GetItemCount(itemId);
        }

        public static int GetItemCount(string itemId, bool includeParties)
        {
            return InventoryHelper.GetItemCount(itemId, includeParties);
        }

        public static ReadOnlyCollection<ItemStack> GetAllInventoryItems()
        {
            return InventoryHelper.GetAllItems();
        }

        public static int GetStorageCapacity()
        {
            return InventoryHelper.GetStorageCapacity();
        }

        public static int GetUsedStorage()
        {
            return InventoryHelper.GetUsedStorage();
        }
    }
}
