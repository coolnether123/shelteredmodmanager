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
                RuntimeWidgetUtil.CreateLabel(parent, emptyText, 540, 32, 20, new Vector3(0f, 76f, 0f), NGUIText.Alignment.Center, depth);
                return;
            }

            List<GameObject> rows = new List<GameObject>();
            for (int i = 0; i < items.Count; i++)
            {
                GameObject row = RuntimeItemRow.Create(parent, items[i], i, depth + i * 3, options);
                if (row != null)
                    rows.Add(row);
            }

            RuntimeScrollView.Attach(parent, rows, 128f, 42f, -150f, 128f, -300f, 300f);
        }
    }
}
