using System;
using UnityEngine;

namespace ShelteredAPI.UI.Internal
{
    internal static class RuntimePanelChrome
    {
        public const int Width = 640;
        public const int Height = 520;

        public static GameObject Create(GameObject root, string title, int depth, Action onClose)
        {
            GameObject panel = RuntimeWidgetUtil.CreateChild(root, "Chrome", Vector3.zero);
            if (panel == null)
                return null;

            RuntimeWidgetUtil.CreateBox(panel, "Frame", Width, Height, new Color(0.08f, 0.09f, 0.1f, 0.96f), Vector3.zero, depth);
            RuntimeWidgetUtil.CreateBox(panel, "Header", Width, 54, new Color(0.13f, 0.16f, 0.18f, 1f), new Vector3(0f, 233f, 0f), depth + 1);
            RuntimeWidgetUtil.CreateLabel(panel, title, 500, 38, 24, new Vector3(-42f, 232f, 0f), NGUIText.Alignment.Left, depth + 2);
            RuntimeButton.Create(panel, "Close", "X", 42, 32, new Vector3(285f, 232f, 0f), depth + 3, onClose);
            return panel;
        }
    }
}
