using System;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.UI.Runtime;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimeItemRow
    {
        public static GameObject Create(
            GameObject parent,
            ContainerUiItem item,
            int index,
            int depth,
            RuntimeItemListOptions options)
        {
            RuntimePanelChromeLayout rowLayout = options != null && options.Layout != null ? options.Layout : RuntimePanelChromeLayout.Default;
            GameObject row = RuntimeWidgetUtil.CreateChild(parent, "ItemRow_" + index, new Vector3(0f, rowLayout.ContentTopY - index * 42f, 0f));
            if (row == null)
                return null;

            RuntimePanelChromeLayout layout = rowLayout;
            RuntimePanelStyle style = options != null ? options.Style : null;
            int rowWidth = Math.Max(320, layout.ContentWidth);
            bool canSelect = CanSelect(item, options);
            bool canTransfer = CanTransfer(item, options);
            Color background = canSelect
                ? new Color(0.12f, 0.13f, 0.14f, 0.94f)
                : new Color(0.08f, 0.08f, 0.08f, 0.62f);
            RuntimeWidgetUtil.CreateBox(row, "Background", rowWidth, 36, background, Vector3.zero, depth);
            string name = item != null && !string.IsNullOrEmpty(item.DisplayName) ? item.DisplayName : item != null ? item.ItemId : string.Empty;
            int nameHeight = item != null && !string.IsNullOrEmpty(item.Subtitle) ? 18 : 28;
            int nameWidth = Math.Max(160, rowWidth - 230);
            float nameX = -rowWidth / 2f + 24f + (nameWidth / 2f);
            RuntimeWidgetUtil.CreateLabel(row, name, nameWidth, nameHeight, 18, new Vector3(nameX, item != null && !string.IsNullOrEmpty(item.Subtitle) ? 5f : -1f, 0f), NGUIText.Alignment.Left, depth + 1, style != null ? style.TextColor : null);
            if (item != null && !string.IsNullOrEmpty(item.Subtitle))
                RuntimeWidgetUtil.CreateLabel(row, item.Subtitle, nameWidth, 14, 14, new Vector3(nameX, -12f, 0f), NGUIText.Alignment.Left, depth + 1, style != null ? style.TextColor : null);

            RuntimeWidgetUtil.CreateLabel(row, FormatCount(item, options), 70, 28, 18, new Vector3(rowWidth / 2f - 124f, -1f, 0f), NGUIText.Alignment.Right, depth + 1, style != null ? style.TextColor : null);
            RuntimeButton.Create(row, "Transfer", "Move", 78, 26, new Vector3(rowWidth / 2f - 48f, 0f, 0f), depth + 2, canTransfer, delegate
            {
                if (options != null && options.OnTransfer != null && item != null)
                {
                    int quantity = options.TransferQuantity > 0 ? options.TransferQuantity : 1;
                    options.OnTransfer(new ContainerUiTransferContext(item, quantity, options.TransferDirection));
                }
            }, style);

            RuntimeWidgetUtil.EnsureCollider(row, rowWidth, 36);
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
