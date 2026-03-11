using System.Collections.Generic;
using UnityEngine;

namespace Cortex.Modules.Shared
{
    internal sealed class PopupMenuSurface
    {
        private const float HeaderHeight = 28f;
        private const float ItemHeight = 24f;
        private const float SeparatorHeight = 8f;
        private const float HorizontalPadding = 8f;
        private const float VerticalPadding = 6f;

        public PopupMenuResult Draw(
            Vector2 position,
            Vector2 viewportSize,
            string headerText,
            IList<PopupMenuItem> items,
            Event current,
            Vector2 localMouse,
            GUIStyle menuStyle,
            GUIStyle buttonStyle,
            GUIStyle headerStyle)
        {
            var result = new PopupMenuResult();
            var menuRect = BuildMenuRect(position, viewportSize, items);
            result.MenuRect = menuRect;

            GUI.Box(menuRect, GUIContent.none, menuStyle);
            GUI.Label(
                new Rect(menuRect.x + HorizontalPadding, menuRect.y + VerticalPadding, menuRect.width - (HorizontalPadding * 2f), 18f),
                headerText ?? string.Empty,
                headerStyle);

            var y = menuRect.y + HeaderHeight;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.IsSeparator)
                {
                    GUI.Box(new Rect(menuRect.x + HorizontalPadding, y + 3f, menuRect.width - (HorizontalPadding * 2f), 1f), GUIContent.none);
                    y += SeparatorHeight;
                    continue;
                }

                var itemRect = new Rect(menuRect.x + 6f, y, menuRect.width - 12f, ItemHeight);
                var previousEnabled = GUI.enabled;
                GUI.enabled = item.Enabled;
                if (GUI.Button(itemRect, GUIContent.none, buttonStyle))
                {
                    result.ActivatedCommandId = item.CommandId ?? string.Empty;
                    result.ShouldClose = true;
                }

                GUI.enabled = previousEnabled;
                GUI.Label(new Rect(itemRect.x + 8f, itemRect.y + 3f, itemRect.width - 70f, 18f), item.Label ?? string.Empty);
                if (!string.IsNullOrEmpty(item.ShortcutText))
                {
                    var gestureRect = new Rect(itemRect.xMax - 78f, itemRect.y + 3f, 70f, 18f);
                    GUI.Label(gestureRect, item.ShortcutText, headerStyle);
                }

                y += ItemHeight + 2f;
            }

            if (current != null && current.type == EventType.MouseDown && !menuRect.Contains(localMouse))
            {
                result.ShouldClose = true;
            }

            return result;
        }

        private static Rect BuildMenuRect(Vector2 position, Vector2 viewportSize, IList<PopupMenuItem> items)
        {
            var itemCount = 0;
            var separatorCount = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }

                if (items[i].IsSeparator)
                {
                    separatorCount++;
                }
                else
                {
                    itemCount++;
                }
            }

            var height = HeaderHeight + (itemCount * (ItemHeight + 2f)) + (separatorCount * SeparatorHeight) + VerticalPadding;
            var x = Mathf.Min(position.x, Mathf.Max(6f, viewportSize.x - 280f - 6f));
            var y = Mathf.Min(position.y, Mathf.Max(6f, viewportSize.y - height - 6f));
            return new Rect(x, y, 280f, height);
        }
    }

    internal sealed class PopupMenuItem
    {
        public string CommandId;
        public string Label;
        public string ShortcutText;
        public bool Enabled;
        public bool IsSeparator;
    }

    internal struct PopupMenuResult
    {
        public bool ShouldClose;
        public string ActivatedCommandId;
        public Rect MenuRect;
    }
}
