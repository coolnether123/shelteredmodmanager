using System;
using ShelteredAPI.Content;
using UnityEngine;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimeFilterTabs
    {
        public static void Build(GameObject parent, ItemCategory[] categories, ItemCategory? selected, Action<ItemCategory?> onChanged, int depth)
        {
            if (parent == null)
                return;

            RuntimeButton.Create(parent, "Filter_All", "All", 76, 28, new Vector3(-260f, 184f, 0f), depth, delegate
            {
                if (onChanged != null) onChanged(null);
            });

            if (categories == null || categories.Length == 0)
                return;

            int max = Math.Min(categories.Length, 6);
            for (int i = 0; i < max; i++)
            {
                ItemCategory category = categories[i];
                int x = -176 + (i * 84);
                RuntimeButton.Create(parent, "Filter_" + category, category.ToString(), 78, 28, new Vector3(x, 184f, 0f), depth, delegate
                {
                    if (onChanged != null) onChanged(category);
                });
            }
        }
    }
}
