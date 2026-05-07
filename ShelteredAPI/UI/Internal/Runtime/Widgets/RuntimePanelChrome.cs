using System;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.UI.FieldManual.Tooltips;
using ShelteredAPI.UI.Runtime;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimePanelChrome
    {
        public const int Width = 640;
        public const int Height = 520;
        public const int HeaderHeight = 54;

        public static GameObject Create(GameObject root, string title, int depth, Action onClose)
        {
            return Create(root, title, depth, onClose, null).Root;
        }

        public static RuntimePanelChromeLayout Create(GameObject root, string title, int depth, Action onClose, RuntimePanelOptions options)
        {
            GameObject panel = RuntimeWidgetUtil.CreateChild(root, "Chrome", Vector3.zero);
            if (panel == null)
                return RuntimePanelChromeLayout.Default;

            RuntimePanelChromeLayout layout = RuntimePanelChromeLayout.FromOptions(options);
            RuntimePanelStyle style = options != null ? options.Style : null;
            Color frameColor = style != null && style.FrameColor.HasValue ? style.FrameColor.Value : new Color(0.08f, 0.09f, 0.1f, 0.96f);
            Color headerColor = style != null && style.HeaderColor.HasValue ? style.HeaderColor.Value : new Color(0.13f, 0.16f, 0.18f, 1f);
            Color? textColor = style != null ? style.TextColor : null;

            RuntimeWidgetUtil.CreateBox(panel, "Frame", layout.Width, layout.Height, frameColor, Vector3.zero, depth);
            RuntimeWidgetUtil.CreateBox(panel, "Header", layout.Width, layout.HeaderHeight, headerColor, new Vector3(0f, layout.HeaderY, 0f), depth + 1);

            int titleWidth = layout.Width - 140;
            float titleX = -42f;
            if (options != null && options.Icon != null)
            {
                RuntimeWidgetUtil.CreateSprite(panel, "Icon", options.Icon, 32, 32, new Vector3(layout.Left + 36f, layout.HeaderY, 0f), depth + 2);
                titleWidth -= 44;
                titleX = layout.Left + 72f + (titleWidth / 2f);
            }

            int titleSize = options != null && options.TitleFontSize > 0 ? options.TitleFontSize : 24;
            RuntimeWidgetUtil.CreateLabel(panel, title, titleWidth, 38, titleSize, new Vector3(titleX, layout.HeaderY - 1f, 0f), NGUIText.Alignment.Left, depth + 2, textColor);
            if (options == null || options.ShowCloseButton)
            {
                string closeText = options != null && !string.IsNullOrEmpty(options.CloseText) ? options.CloseText : "X";
                RuntimeButton.Create(panel, "Close", closeText, 42, 32, new Vector3(layout.Right - 35f, layout.HeaderY - 1f, 0f), depth + 3, true, onClose, style);
            }

            layout.Root = panel;
            return layout;
        }
    }

    internal sealed class RuntimePanelChromeLayout
    {
        public GameObject Root;
        public int Width;
        public int Height;
        public int HeaderHeight;
        public float Left;
        public float Right;
        public float Top;
        public float Bottom;
        public float HeaderY;
        public float FilterY;
        public float ContentTopY;
        public float FooterY;
        public int ContentWidth;

        public static RuntimePanelChromeLayout Default
        {
            get { return FromOptions(null); }
        }

        public static RuntimePanelChromeLayout FromOptions(RuntimePanelOptions options)
        {
            int width = options != null && options.Width > 0 ? options.Width : RuntimePanelChrome.Width;
            int height = options != null && options.Height > 0 ? options.Height : RuntimePanelChrome.Height;
            int headerHeight = options != null && options.HeaderHeight > 0 ? options.HeaderHeight : RuntimePanelChrome.HeaderHeight;
            width = Math.Max(360, width);
            height = Math.Max(280, height);
            headerHeight = Math.Max(42, Math.Min(headerHeight, height / 3));

            RuntimePanelChromeLayout layout = new RuntimePanelChromeLayout();
            layout.Width = width;
            layout.Height = height;
            layout.HeaderHeight = headerHeight;
            layout.Left = -width / 2f;
            layout.Right = width / 2f;
            layout.Top = height / 2f;
            layout.Bottom = -height / 2f;
            layout.HeaderY = layout.Top - (headerHeight / 2f);
            layout.FilterY = layout.HeaderY - 49f;
            layout.ContentTopY = layout.FilterY - 56f;
            layout.FooterY = layout.Bottom + 34f;
            layout.ContentWidth = Math.Max(260, width - 80);
            return layout;
        }
    }
}
