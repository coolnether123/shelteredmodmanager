using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Content;
using UnityEngine;
namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioInventoryItemCatalogEntry
    {
        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public string Detail { get; set; }
        public ItemManager.ItemType ItemType { get; set; }
        public ItemManager.ItemCategory Category { get; set; }
        public Sprite PreviewSprite { get; set; }
        public bool PreviewCreatedByCustomScenarioEditor { get; set; }
    }

    internal static class ScenarioInventoryItemCatalog
    {
        private static List<ScenarioInventoryItemCatalogEntry> CachedEntries;
        private static int CachedRuntimeSignature = int.MinValue;

        public static List<ScenarioInventoryItemCatalogEntry> Build()
        {
            int runtimeSignature = ComputeRuntimeSignature();
            if (CachedEntries != null && CachedRuntimeSignature == runtimeSignature)
                return CachedEntries;

            List<ItemManager.ItemType> types = BuildCatalogTypes();
            List<ScenarioInventoryItemCatalogEntry> entries = new List<ScenarioInventoryItemCatalogEntry>();
            List<string> missingPreviews = new List<string>();
            for (int i = 0; i < types.Count; i++)
            {
                ScenarioInventoryItemCatalogEntry entry = CreateEntry(types[i]);
                if (entry != null)
                {
                    entries.Add(entry);
                    if (entry.PreviewSprite == null)
                        missingPreviews.Add(entry.ItemId);
                }
            }

            entries.Sort(delegate(ScenarioInventoryItemCatalogEntry left, ScenarioInventoryItemCatalogEntry right)
            {
                return string.Compare(
                    left != null ? left.DisplayName : null,
                    right != null ? right.DisplayName : null,
                    StringComparison.OrdinalIgnoreCase);
            });
            CachedRuntimeSignature = runtimeSignature;
            CachedEntries = entries;
            MMLog.WriteInfo("[ScenarioInventoryItemCatalog] Catalog ready. entries=" + entries.Count
                + " missingPreviews=" + missingPreviews.Count
                + (missingPreviews.Count > 0 ? " ids=" + string.Join(",", missingPreviews.ToArray()) : string.Empty));
            return CachedEntries;
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

        private static List<ItemManager.ItemType> BuildCatalogTypes()
        {
            Dictionary<ItemManager.ItemType, bool> seen = new Dictionary<ItemManager.ItemType, bool>();
            List<ItemManager.ItemType> types = new List<ItemManager.ItemType>();

            ItemManager manager = ItemManager.Instance;
            List<ItemManager.ItemType> defined = manager != null ? manager.GetAllDefinedItems() : null;
            for (int i = 0; defined != null && i < defined.Count; i++)
                AddType(types, seen, defined[i]);

            foreach (ItemManager.ItemType type in ContentInjector.RegisteredTypes)
                AddType(types, seen, type);

            // ItemManager only exposes prefabs that were registered in ItemDefs.
            // The enum also contains unique, scenario-only, and unfinished records
            // that remain valid in saves (for example Weapon_M16). Always merge the
            // enum instead of using it only when the runtime manager is empty.
            Array values = Enum.GetValues(typeof(ItemManager.ItemType));
            for (int i = 0; i < values.Length; i++)
                AddType(types, seen, (ItemManager.ItemType)values.GetValue(i));

            return types;
        }

        private static void AddType(List<ItemManager.ItemType> types, Dictionary<ItemManager.ItemType, bool> seen, ItemManager.ItemType type)
        {
            if (type == ItemManager.ItemType.Undefined || seen.ContainsKey(type))
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
            if (category == ItemManager.ItemCategory.Undefined)
                category = ItemManager.ItemCategory.Normal;

            string itemId = GetStableItemId(type);
            bool previewCreatedByCustomScenarioEditor;
            Sprite previewSprite = ScenarioInventoryItemPreviewResolver.Resolve(
                type,
                definition,
                out previewCreatedByCustomScenarioEditor);
            return new ScenarioInventoryItemCatalogEntry
            {
                ItemId = itemId,
                DisplayName = ResolveDisplayName(definition, itemId, type),
                Detail = itemId
                    + " | " + category
                    + (definition == null ? " | Enum-only game record" : string.Empty)
                    + (previewCreatedByCustomScenarioEditor ? " | Sprite made by Custom Scenario Editor" : string.Empty),
                ItemType = type,
                Category = category,
                PreviewSprite = previewSprite,
                PreviewCreatedByCustomScenarioEditor = previewCreatedByCustomScenarioEditor
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

        private static int ComputeRuntimeSignature()
        {
            unchecked
            {
                int hash = 17;
                ItemManager manager = ItemManager.Instance;
                hash = (hash * 31) + (manager != null ? manager.GetInstanceID() : 0);
                hash = (hash * 31) + (ObjectManager.Instance != null ? ObjectManager.Instance.GetInstanceID() : 0);

                List<ItemManager.ItemType> defined = manager != null ? manager.GetAllDefinedItems() : null;
                hash = (hash * 31) + (defined != null ? defined.Count : 0);
                for (int i = 0; defined != null && i < defined.Count; i++)
                    hash ^= ((int)defined[i] * 397);

                int registeredCount = 0;
                foreach (ItemManager.ItemType type in ContentInjector.RegisteredTypes)
                {
                    registeredCount++;
                    hash ^= ((int)type * 7919);
                }

                return (hash * 31) + registeredCount;
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
