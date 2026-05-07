using System;
using System.Collections.Generic;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.UI.Runtime;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimeItemList
    {
        public static void Build(
            GameObject parent,
            IList<ContainerUiItem> items,
            int depth,
            RuntimeItemListOptions options)
        {
            if (parent == null)
                return;

            RuntimeWidgetUtil.DestroyChildren(parent);

            if (items == null || items.Count == 0)
            {
                string emptyText = options != null && !string.IsNullOrEmpty(options.EmptyText) ? options.EmptyText : "No items";
                RuntimePanelChromeLayout emptyLayout = options != null && options.Layout != null ? options.Layout : RuntimePanelChromeLayout.Default;
                Color? textColor = options != null && options.Style != null ? options.Style.TextColor : null;
                RuntimeWidgetUtil.CreateLabel(parent, emptyText, emptyLayout.ContentWidth, 32, 20, new Vector3(0f, emptyLayout.ContentTopY - 52f, 0f), NGUIText.Alignment.Center, depth, textColor);
                return;
            }

            List<GameObject> rows = new List<GameObject>();
            for (int i = 0; i < items.Count; i++)
            {
                GameObject row = RuntimeItemRow.Create(parent, items[i], i, depth + i * 3, options);
                if (row != null)
                    rows.Add(row);
            }

            RuntimePanelChromeLayout layout = options != null && options.Layout != null ? options.Layout : RuntimePanelChromeLayout.Default;
            RuntimeScrollView.Attach(parent, rows, layout.ContentTopY, 42f, layout.Bottom + 110f, layout.ContentTopY, layout.Left + 20f, layout.Right - 20f);
        }
    }
}
