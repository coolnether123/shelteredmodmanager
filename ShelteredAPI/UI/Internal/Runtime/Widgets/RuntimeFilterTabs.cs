using System;
using ShelteredAPI.Content;
using ShelteredAPI.UI.Runtime;
using UnityEngine;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimeFilterTabs
    {
        public static void Build(GameObject parent, ItemCategory[] categories, ItemCategory? selected, Action<ItemCategory?> onChanged, int depth)
        {
            Build(parent, categories, selected, onChanged, depth, RuntimePanelChromeLayout.Default, null);
        }

        public static void Build(GameObject parent, ItemCategory[] categories, ItemCategory? selected, Action<ItemCategory?> onChanged, int depth, RuntimePanelChromeLayout layout, RuntimePanelStyle style)
        {
            if (parent == null)
                return;

            RuntimePanelChromeLayout usedLayout = layout ?? RuntimePanelChromeLayout.Default;
            float x = usedLayout.Left + 60f;
            RuntimeButton.Create(parent, "Filter_All", "All", 76, 28, new Vector3(x, usedLayout.FilterY, 0f), depth, true, delegate
            {
                if (onChanged != null) onChanged(null);
            }, style);

            if (categories == null || categories.Length == 0)
                return;

            int max = Math.Min(categories.Length, 6);
            for (int i = 0; i < max; i++)
            {
                ItemCategory category = categories[i];
                ItemCategory captured = category;
                float tabX = usedLayout.Left + 144f + (i * 84);
                RuntimeButton.Create(parent, "Filter_" + category, category.ToString(), 78, 28, new Vector3(tabX, usedLayout.FilterY, 0f), depth, true, delegate
                {
                    if (onChanged != null) onChanged(captured);
                }, style);
            }
        }
    }
}
