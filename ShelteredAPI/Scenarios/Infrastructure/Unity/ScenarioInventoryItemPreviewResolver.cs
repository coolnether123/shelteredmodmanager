using System;
using System.Collections.Generic;
using ShelteredAPI.Content;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity
{
    /// <summary>
    /// Resolves editor previews without changing gameplay item definitions.
    /// Native item-atlas art wins, followed by the item's real world prefab and
    /// finally a small set of native variant aliases for quality-tier records.
    /// </summary>
    internal static class ScenarioInventoryItemPreviewResolver
    {
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public static Sprite Resolve(ItemManager.ItemType type, ItemDefinition definition)
        {
            Sprite customIcon;
            if (ContentInjector.TryGetResolvedItemIcon(type, out customIcon))
                return customIcon;

            Sprite itemIcon = ResolveItemAtlasSprite(definition);
            if (itemIcon != null)
                return itemIcon;

            Sprite objectPreview = ResolveObjectPrefabSprite(type, definition);
            if (objectPreview != null)
                return objectPreview;

            ItemManager.ItemType nativeAlias;
            if (TryGetNativeVariantAlias(type, out nativeAlias))
            {
                ItemDefinition aliasDefinition = ItemManager.Instance != null
                    ? ItemManager.Instance.GetItemDefinition(nativeAlias)
                    : null;
                return ResolveItemAtlasSprite(aliasDefinition);
            }

            return null;
        }

        private static Sprite ResolveItemAtlasSprite(ItemDefinition definition)
        {
            UISprite uiSprite = definition != null ? definition.GetComponent<UISprite>() : null;
            if (uiSprite == null || uiSprite.atlas == null || string.IsNullOrEmpty(uiSprite.spriteName))
                return null;

            UISpriteData spriteData = uiSprite.atlas.GetSprite(uiSprite.spriteName);
            Texture2D texture = uiSprite.atlas.texture as Texture2D;
            if (spriteData == null || texture == null || spriteData.width <= 0 || spriteData.height <= 0)
                return null;

            string cacheKey = texture.GetInstanceID().ToString() + ":" + spriteData.name + ":" + spriteData.x + ":" + spriteData.y + ":" + spriteData.width + ":" + spriteData.height;
            Sprite cached;
            if (SpriteCache.TryGetValue(cacheKey, out cached) && cached != null)
                return cached;

            float y = texture.height - spriteData.y - spriteData.height;
            if (y < 0f || y + spriteData.height > texture.height)
                y = spriteData.y;
            if (spriteData.x < 0 || spriteData.x + spriteData.width > texture.width || y < 0f || y + spriteData.height > texture.height)
                return null;

            try
            {
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(spriteData.x, y, spriteData.width, spriteData.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = "ScenarioInventory_" + spriteData.name;
                SpriteCache[cacheKey] = sprite;
                return sprite;
            }
            catch
            {
                return null;
            }
        }

        private static Sprite ResolveObjectPrefabSprite(ItemManager.ItemType itemType, ItemDefinition definition)
        {
            if (ObjectManager.Instance == null)
                return null;

            ObjectManager.ObjectType objectType = ResolveObjectType(itemType, definition);
            if (objectType == ObjectManager.ObjectType.Undefined)
                return null;

            int level = definition != null ? Math.Max(1, definition.ObjectLevel) : ResolveLegacyObjectLevel(itemType);
            GameObject prefab = ObjectManager.Instance.GetPrefab(objectType, level);
            SpriteRenderer[] renderers = prefab != null ? prefab.GetComponentsInChildren<SpriteRenderer>(true) : null;
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sprite != null)
                    return renderers[i].sprite;
            }

            return null;
        }

        private static ObjectManager.ObjectType ResolveObjectType(ItemManager.ItemType itemType, ItemDefinition definition)
        {
            if (definition != null && definition.ObjectType != ObjectManager.ObjectType.Undefined)
                return definition.ObjectType;

            string itemName = itemType.ToString();
            if (!itemName.StartsWith("Object_", StringComparison.Ordinal))
                return ObjectManager.ObjectType.Undefined;

            string objectName = itemName.Substring("Object_".Length);
            switch (objectName)
            {
                case "Bookcase":
                    objectName = "Bookshelf";
                    break;
                case "Storage":
                case "CardboardBox":
                case "SmallCrate":
                case "MediumCrate":
                case "LargeCrate":
                    objectName = "StorageArea";
                    break;
                case "SingleBed2":
                    objectName = "SingleBed";
                    break;
            }

            try
            {
                return (ObjectManager.ObjectType)Enum.Parse(typeof(ObjectManager.ObjectType), objectName, true);
            }
            catch
            {
                return ObjectManager.ObjectType.Undefined;
            }
        }

        private static int ResolveLegacyObjectLevel(ItemManager.ItemType itemType)
        {
            switch (itemType)
            {
                case ItemManager.ItemType.Object_SingleBed2:
                    return 2;
                default:
                    return 1;
            }
        }

        private static bool TryGetNativeVariantAlias(ItemManager.ItemType type, out ItemManager.ItemType alias)
        {
            switch (type)
            {
                case ItemManager.ItemType.PoorNailBomb:
                case ItemManager.ItemType.ExcellentNailBomb:
                case ItemManager.ItemType.Combat_Nailbomb:
                    alias = ItemManager.ItemType.NailBomb;
                    return true;
                case ItemManager.ItemType.Combat_PoorFlashBang:
                case ItemManager.ItemType.Combat_ExcellentFlashBang:
                    alias = ItemManager.ItemType.Combat_FlashBang;
                    return true;
                default:
                    alias = ItemManager.ItemType.Undefined;
                    return false;
            }
        }
    }
}
