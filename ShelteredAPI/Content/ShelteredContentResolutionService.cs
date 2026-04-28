using System.Collections.Generic;
using ModAPI.Core;

namespace ShelteredAPI.Content
{
    internal sealed class ShelteredContentResolutionService : IContentResolutionService
    {
        public bool TryResolveRuntimeItemKey(string itemId, out object runtimeItemKey)
        {
            ItemManager.ItemType itemType;
            if (ContentInjector.ResolveItemType(itemId, out itemType))
            {
                runtimeItemKey = itemType;
                return true;
            }

            runtimeItemKey = null;
            return false;
        }

        public IEnumerable<object> GetRegisteredRuntimeItemKeys()
        {
            foreach (ItemManager.ItemType itemType in ContentInjector.RegisteredTypes)
            {
                yield return itemType;
            }
        }
    }
}
