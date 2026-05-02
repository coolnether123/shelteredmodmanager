using System.Collections.Generic;
using ModAPI.Core;

namespace ShelteredAPI.Content
{
    /// <summary>
    /// Sheltered-owned typed adapter for compatibility APIs that still expose Sheltered item runtime types.
    /// New code should use <see cref="IContentResolutionService"/> and keep host runtime keys opaque.
    /// </summary>
    internal static class ShelteredItemContentBridge
    {
        private static readonly ItemManager.ItemType[] EmptyRegisteredTypes = new ItemManager.ItemType[0];
        private static readonly IContentResolutionService NullService = new NullContentResolutionService();
        private static readonly object[] EmptyRuntimeItemKeys = new object[0];

        internal static IEnumerable<ItemManager.ItemType> GetRegisteredTypes()
        {
            List<ItemManager.ItemType> itemTypes = null;

            foreach (object runtimeItemKey in GetRegisteredRuntimeItemKeys())
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
            if (TryResolveRuntimeItemKey(itemId, out runtimeItemKey))
                return TryConvertRuntimeItemKey(runtimeItemKey, out type);

            type = ItemManager.ItemType.Undefined;
            return false;
        }

        private static bool TryResolveRuntimeItemKey(string itemId, out object runtimeItemKey)
        {
            try
            {
                return Current.TryResolveRuntimeItemKey(itemId, out runtimeItemKey);
            }
            catch (System.Exception ex)
            {
                runtimeItemKey = null;
                MMLog.WarnOnce("ShelteredItemContentBridge.TryResolveRuntimeItemKey", "Content resolution failed: " + ex.Message);
                return false;
            }
        }

        private static IEnumerable<object> GetRegisteredRuntimeItemKeys()
        {
            try
            {
                IEnumerable<object> keys = Current.GetRegisteredRuntimeItemKeys();
                return keys ?? EmptyRuntimeItemKeys;
            }
            catch (System.Exception ex)
            {
                MMLog.WarnOnce("ShelteredItemContentBridge.GetRegisteredRuntimeItemKeys", "Content enumeration failed: " + ex.Message);
                return EmptyRuntimeItemKeys;
            }
        }

        private static IContentResolutionService Current
        {
            get
            {
                if (!ModAPIRegistry.IsAPIRegistered(GameRuntimeApiIds.ContentResolution))
                    return NullService;

                IContentResolutionService service = ModAPIRegistry.GetAPI<IContentResolutionService>(GameRuntimeApiIds.ContentResolution);
                return service ?? NullService;
            }
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

        private sealed class NullContentResolutionService : IContentResolutionService
        {
            public bool TryResolveRuntimeItemKey(string itemId, out object runtimeItemKey)
            {
                runtimeItemKey = null;
                return false;
            }

            public IEnumerable<object> GetRegisteredRuntimeItemKeys()
            {
                return EmptyRuntimeItemKeys;
            }
        }
    }
}
