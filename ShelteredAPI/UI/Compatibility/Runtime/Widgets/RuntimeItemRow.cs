using System;
using UnityEngine;

namespace ShelteredAPI.UI.Internal
{
    internal static class RuntimeItemRow
    {
        public static GameObject Create(
            GameObject parent,
            ContainerUiItem item,
            int index,
            int depth,
            RuntimeItemListOptions options)
        {
            GameObject row = RuntimeWidgetUtil.CreateChild(parent, "ItemRow_" + index, new Vector3(0f, 128f - index * 42f, 0f));
            if (row == null)
                return null;

            bool canSelect = CanSelect(item, options);
            bool canTransfer = CanTransfer(item, options);
            Color background = canSelect
                ? new Color(0.12f, 0.13f, 0.14f, 0.94f)
                : new Color(0.08f, 0.08f, 0.08f, 0.62f);
            RuntimeWidgetUtil.CreateBox(row, "Background", 560, 36, background, Vector3.zero, depth);
            string name = item != null && !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName : item != null ? item.ItemId : string.Empty;
            int nameHeight = item != null && !string.IsNullOrEmpty(item.Subtitle) ? 18 : 28;
            RuntimeWidgetUtil.CreateLabel(row, name, 330, nameHeight, 18, new Vector3(-104f, item != null && !string.IsNullOrEmpty(item.Subtitle) ? 5f : -1f, 0f), NGUIText.Alignment.Left, depth + 1);
            if (item != null && !string.IsNullOrEmpty(item.Subtitle))
                RuntimeWidgetUtil.CreateLabel(row, item.Subtitle, 330, 14, 14, new Vector3(-104f, -12f, 0f), NGUIText.Alignment.Left, depth + 1);

            RuntimeWidgetUtil.CreateLabel(row, FormatCount(item, options), 70, 28, 18, new Vector3(145f, -1f, 0f), NGUIText.Alignment.Right, depth + 1);
            RuntimeButton.Create(row, "Transfer", "Move", 78, 26, new Vector3(232f, 0f, 0f), depth + 2, canTransfer, delegate
            {
                if (options != null && options.OnTransfer != null && item != null)
                {
                    int quantity = options.TransferQuantity > 0 ? options.TransferQuantity : 1;
                    options.OnTransfer(new ContainerUiTransferContext(item, quantity, options.TransferDirection));
                }
            });

            RuntimeWidgetUtil.EnsureCollider(row, 560, 36);
            UIEventListener listener = UIEventListener.Get(row);
            listener.onClick = delegate
            {
                if (canSelect && options != null && options.OnSelected != null && item != null)
                    options.OnSelected(item);
            };

            return row;
        }

        private static bool CanSelect(ContainerUiItem item, RuntimeItemListOptions options)
        {
            if (item == null)
                return false;
            if (item.IsEnabled.HasValue && !item.IsEnabled.Value)
                return false;
            return options == null || options.CanSelect == null || options.CanSelect(item);
        }

        private static bool CanTransfer(ContainerUiItem item, RuntimeItemListOptions options)
        {
            if (item == null)
                return false;
            if (item.IsTransferEnabled.HasValue && !item.IsTransferEnabled.Value)
                return false;
            return options != null && options.OnTransfer != null && (options.CanTransfer == null || options.CanTransfer(item));
        }

        private static string FormatCount(ContainerUiItem item, RuntimeItemListOptions options)
        {
            if (item == null)
                return "0";
            if (!string.IsNullOrEmpty(item.CountText))
                return item.CountText;
            if (options != null && options.FormatCount != null)
                return options.FormatCount(item);
            return item.Count.ToString();
        }
    }
}
