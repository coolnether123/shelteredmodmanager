using System;
using System.Collections.Generic;
using ShelteredAPI.Content;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioInventoryItemCatalogEntry
    {
        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public string Detail { get; set; }
        public ItemManager.ItemType ItemType { get; set; }
        public ItemManager.ItemCategory Category { get; set; }
        public Sprite PreviewSprite { get; set; }
    }

    internal static class ScenarioInventoryItemCatalog
    {
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public static List<ScenarioInventoryItemCatalogEntry> Build()
        {
            List<ItemManager.ItemType> types = BuildDefinedTypes();
            List<ScenarioInventoryItemCatalogEntry> entries = new List<ScenarioInventoryItemCatalogEntry>();
            for (int i = 0; i < types.Count; i++)
            {
                ScenarioInventoryItemCatalogEntry entry = CreateEntry(types[i]);
                if (entry != null)
                    entries.Add(entry);
            }

            entries.Sort(delegate(ScenarioInventoryItemCatalogEntry left, ScenarioInventoryItemCatalogEntry right)
            {
                return string.Compare(
                    left != null ? left.DisplayName : null,
                    right != null ? right.DisplayName : null,
                    StringComparison.OrdinalIgnoreCase);
            });
            return entries;
        }

        public static ScenarioInventoryItemCatalogEntry Resolve(string itemId)
        {
            ItemManager.ItemType type;
            if (!ContentInjector.ResolveItemType(itemId, out type))
                return CreateUnknownEntry(itemId);

            ScenarioInventoryItemCatalogEntry entry = CreateEntry(type);
            return entry ?? CreateUnknownEntry(itemId);
        }

        public static ScenarioInventoryItemCatalogEntry Resolve(ItemManager.ItemType type)
        {
            ScenarioInventoryItemCatalogEntry entry = CreateEntry(type);
            return entry ?? CreateUnknownEntry(type.ToString());
        }

        public static string DefaultItemId()
        {
            ItemManager.ItemType[] preferred =
            {
                ItemManager.ItemType.Ration,
                ItemManager.ItemType.Water,
                ItemManager.ItemType.Wood
            };

            for (int i = 0; i < preferred.Length; i++)
            {
                ScenarioInventoryItemCatalogEntry entry = CreateEntry(preferred[i]);
                if (entry != null)
                    return entry.ItemId;
            }

            List<ScenarioInventoryItemCatalogEntry> catalog = Build();
            return catalog.Count > 0 ? catalog[0].ItemId : ItemManager.ItemType.Ration.ToString();
        }

        public static string CycleItemId(string currentItemId, int delta)
        {
            List<ScenarioInventoryItemCatalogEntry> catalog = Build();
            if (catalog.Count == 0)
                return string.IsNullOrEmpty(currentItemId) ? DefaultItemId() : currentItemId;

            int current = IndexOf(catalog, currentItemId);
            int next = current < 0 ? 0 : Wrap(current + delta, catalog.Count);
            return catalog[next].ItemId;
        }

        public static string GetStableItemId(ItemManager.ItemType type)
        {
            string customId;
            if (ContentInjector.TryGetResolvedItemId(type, out customId))
                return customId;
            return type.ToString();
        }

        private static List<ItemManager.ItemType> BuildDefinedTypes()
        {
            Dictionary<ItemManager.ItemType, bool> seen = new Dictionary<ItemManager.ItemType, bool>();
            List<ItemManager.ItemType> types = new List<ItemManager.ItemType>();

            ItemManager manager = ItemManager.Instance;
            List<ItemManager.ItemType> defined = manager != null ? manager.GetAllDefinedItems() : null;
            for (int i = 0; defined != null && i < defined.Count; i++)
                AddType(types, seen, defined[i]);

            foreach (ItemManager.ItemType type in ContentInjector.RegisteredTypes)
                AddType(types, seen, type);

            if (types.Count == 0)
            {
                Array values = Enum.GetValues(typeof(ItemManager.ItemType));
                for (int i = 0; i < values.Length; i++)
                    AddType(types, seen, (ItemManager.ItemType)values.GetValue(i));
            }

            return types;
        }

        private static void AddType(List<ItemManager.ItemType> types, Dictionary<ItemManager.ItemType, bool> seen, ItemManager.ItemType type)
        {
            if (type == ItemManager.ItemType.Undefined || seen.ContainsKey(type))
                return;

            ScenarioInventoryItemCatalogEntry entry = CreateEntry(type);
            if (entry == null)
                return;

            seen[type] = true;
            types.Add(type);
        }

        private static ScenarioInventoryItemCatalogEntry CreateEntry(ItemManager.ItemType type)
        {
            if (type == ItemManager.ItemType.Undefined)
                return null;

            ItemDefinition definition = ItemManager.Instance != null ? ItemManager.Instance.GetItemDefinition(type) : null;
            ItemManager.ItemCategory category = definition != null ? definition.Category : ItemManager.ItemCategory.Normal;
            if (category == ItemManager.ItemCategory.Undefined || category == ItemManager.ItemCategory.Object)
                return null;

            string itemId = GetStableItemId(type);
            return new ScenarioInventoryItemCatalogEntry
            {
                ItemId = itemId,
                DisplayName = ResolveDisplayName(definition, itemId, type),
                Detail = itemId + " | " + category,
                ItemType = type,
                Category = category,
                PreviewSprite = ResolvePreviewSprite(type, definition)
            };
        }

        private static ScenarioInventoryItemCatalogEntry CreateUnknownEntry(string itemId)
        {
            string value = string.IsNullOrEmpty(itemId) ? "<missing item>" : itemId;
            return new ScenarioInventoryItemCatalogEntry
            {
                ItemId = value,
                DisplayName = value,
                Detail = "Unknown item",
                ItemType = ItemManager.ItemType.Undefined,
                Category = ItemManager.ItemCategory.Undefined
            };
        }

        private static string ResolveDisplayName(ItemDefinition definition, string itemId, ItemManager.ItemType type)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.NameLocalizationKey))
            {
                try
                {
                    string localized = Localization.Get(definition.NameLocalizationKey);
                    if (!string.IsNullOrEmpty(localized) && !string.Equals(localized, definition.NameLocalizationKey, StringComparison.Ordinal))
                        return localized;
                }
                catch
                {
                }
            }

            string fallback = type != ItemManager.ItemType.Undefined ? type.ToString() : itemId;
            return SplitIdentifier(fallback);
        }

        private static Sprite ResolvePreviewSprite(ItemManager.ItemType type, ItemDefinition definition)
        {
            Sprite customIcon;
            if (ContentInjector.TryGetResolvedItemIcon(type, out customIcon))
                return customIcon;

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
                Rect rect = new Rect(spriteData.x, y, spriteData.width, spriteData.height);
                Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
                sprite.name = "ScenarioInventory_" + spriteData.name;
                SpriteCache[cacheKey] = sprite;
                return sprite;
            }
            catch
            {
                return null;
            }
        }

        private static int IndexOf(List<ScenarioInventoryItemCatalogEntry> catalog, string itemId)
        {
            ItemManager.ItemType currentType;
            bool hasType = ContentInjector.ResolveItemType(itemId, out currentType);
            for (int i = 0; catalog != null && i < catalog.Count; i++)
            {
                ScenarioInventoryItemCatalogEntry entry = catalog[i];
                if (entry == null)
                    continue;

                if (string.Equals(entry.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    return i;
                if (hasType && entry.ItemType == currentType)
                    return i;
            }
            return -1;
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0)
                return 0;
            while (value < 0)
                value += count;
            while (value >= count)
                value -= count;
            return value;
        }

        private static string SplitIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = value.Replace('_', ' ').Replace('.', ' ');
            List<char> chars = new List<char>();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(value[i - 1]))
                    chars.Add(' ');
                chars.Add(c);
            }
            return new string(chars.ToArray());
        }
    }
}
