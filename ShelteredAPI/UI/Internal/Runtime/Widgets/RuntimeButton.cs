using System;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.UI.Runtime;
namespace ShelteredAPI.UI.Internal.Runtime.Widgets{
    internal static class RuntimeButton
    {
        public static GameObject Create(GameObject parent, string name, string text, int width, int height, Vector3 localPosition, int depth, Action onClick)
        {
            return Create(parent, name, text, width, height, localPosition, depth, true, onClick);
        }

        public static GameObject Create(GameObject parent, string name, string text, int width, int height, Vector3 localPosition, int depth, bool enabled, Action onClick)
        {
            return Create(parent, name, text, width, height, localPosition, depth, enabled, onClick, null);
        }

        public static GameObject Create(GameObject parent, string name, string text, int width, int height, Vector3 localPosition, int depth, bool enabled, Action onClick, RuntimePanelStyle style)
        {
            GameObject button = RuntimeWidgetUtil.CreateChild(parent, name, localPosition);
            if (button == null)
                return null;

            Color color = enabled
                ? new Color(0.18f, 0.2f, 0.22f, 0.96f)
                : new Color(0.12f, 0.12f, 0.12f, 0.62f);
            if (style != null)
            {
                if (enabled && style.ButtonColor.HasValue)
                    color = style.ButtonColor.Value;
                else if (!enabled && style.DisabledButtonColor.HasValue)
                    color = style.DisabledButtonColor.Value;
            }
            RuntimeWidgetUtil.CreateBox(button, "Background", width, height, color, Vector3.zero, depth);
            RuntimeWidgetUtil.CreateLabel(button, text, width - 12, height - 4, 18, new Vector3(0f, -1f, 0f), NGUIText.Alignment.Center, depth + 1, style != null ? style.TextColor : null);
            RuntimeWidgetUtil.EnsureCollider(button, width, height);

            UIEventListener listener = UIEventListener.Get(button);
            listener.onClick = delegate
            {
                if (enabled && onClick != null)
                    onClick();
            };
            return button;
        }
    }
}
