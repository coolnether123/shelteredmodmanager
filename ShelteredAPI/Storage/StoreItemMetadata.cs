using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Persistence;
using ShelteredAPI.UI.Runtime;
using UnityEngine;
using GameItemDefinition = global::ItemDefinition;

namespace ShelteredAPI.Storage
{
    internal static class StoreItemMetadata
    {
        public static string DisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return string.Empty;

            ItemManager.ItemType type;
            if (ShelteredContent.Runtime.ResolveItemType(itemId, out type) && ItemManager.Instance != null)
            {
                GameItemDefinition definition = ItemManager.Instance.GetItemDefinition(type);
                if (definition != null && !string.IsNullOrEmpty(definition.NameLocalizationKey))
                {
                    try
                    {
                        string localized = Localization.Get(definition.NameLocalizationKey);
                        if (!string.IsNullOrEmpty(localized))
                            return localized;
                    }
                    catch
                    {
                    }
                }
            }

            return itemId;
        }

        public static ItemCategory Category(string itemId)
        {
            ItemManager.ItemType type;
            if (ShelteredContent.Runtime.ResolveItemType(itemId, out type) && ItemManager.Instance != null)
            {
                GameItemDefinition definition = ItemManager.Instance.GetItemDefinition(type);
                if (definition != null)
                    return (ItemCategory)(int)definition.Category;
            }

            if (InventoryItemStore.IsSpecialFood(itemId))
                return ItemCategory.Food;

            return ItemCategory.Normal;
        }
    }
}
