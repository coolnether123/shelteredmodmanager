using System.Collections.Generic;

namespace ModAPI.Core
{
    /// <summary>
    /// Temporary typed adapter for compatibility APIs that still expose Sheltered item runtime types.
    /// New code should use <see cref="IContentResolutionService"/> and keep host runtime keys opaque.
    /// </summary>
    internal static class ShelteredContentBridge
    {
        private static readonly ItemManager.ItemType[] EmptyRegisteredTypes = new ItemManager.ItemType[0];

        internal static IEnumerable<ItemManager.ItemType> GetRegisteredTypes()
        {
            List<ItemManager.ItemType> itemTypes = null;

            foreach (object runtimeItemKey in ContentResolutionServices.GetRegisteredRuntimeItemKeys())
            {
                ItemManager.ItemType itemType;
                if (!TryConvertRuntimeItemKey(runtimeItemKey, out itemType))
                    continue;

                if (itemTypes == null)
                    itemTypes = new List<ItemManager.ItemType>();

                itemTypes.Add(itemType);
            }

            if (itemTypes != null)
                return itemTypes;

            return EmptyRegisteredTypes;
        }

        internal static bool ResolveItemType(string itemId, out ItemManager.ItemType type)
        {
            object runtimeItemKey;
            if (ContentResolutionServices.TryResolveRuntimeItemKey(itemId, out runtimeItemKey))
                return TryConvertRuntimeItemKey(runtimeItemKey, out type);

            type = ItemManager.ItemType.Undefined;
            return false;
        }

        private static bool TryConvertRuntimeItemKey(object runtimeItemKey, out ItemManager.ItemType itemType)
        {
            if (runtimeItemKey is ItemManager.ItemType)
            {
                itemType = (ItemManager.ItemType)runtimeItemKey;
                return true;
            }

            itemType = ItemManager.ItemType.Undefined;
            return false;
        }
    }
}
