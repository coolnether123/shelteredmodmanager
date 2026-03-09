using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cortex.Chrome
{
    internal sealed class CortexWindowAction
    {
        public string ActionId;
        public string Label;
        public float Width;
        public Action Execute;
    }

    internal static class CortexWindowChromeController
    {
        private const float ResizeHandleSize = 18f;
        private static int _resizingWindowId = -1;
        private static Rect _resizeStartRect = new Rect(0f, 0f, 0f, 0f);
        private static Vector2 _resizeStartMouse = Vector2.zero;

        public static Rect BuildCollapsedRect(Rect expandedRect, float width, float height)
        {
            return new Rect(expandedRect.x, expandedRect.y, width, height);
        }

        public static Rect ToggleCollapsed(CortexWindowChromeState state, Rect currentRect, float collapsedWidth, float collapsedHeight)
        {
            if (state == null)
            {
                return currentRect;
            }

            if (!state.IsCollapsed)
            {
                state.ExpandedRect = currentRect;
                state.CollapsedRect = BuildCollapsedRect(currentRect, collapsedWidth, collapsedHeight);
                state.IsCollapsed = true;
                return state.CollapsedRect;
            }

            state.IsCollapsed = false;
            if (state.ExpandedRect.width <= 0f || state.ExpandedRect.height <= 0f)
            {
                state.ExpandedRect = currentRect;
            }

            return state.ExpandedRect;
        }

        public static Rect DrawResizeHandle(int windowId, Rect windowRect, float minWidth, float minHeight, float maxWidth, float maxHeight)
        {
            var handleRect = new Rect(windowRect.width - ResizeHandleSize - 4f, windowRect.height - ResizeHandleSize - 4f, ResizeHandleSize, ResizeHandleSize);
            GUI.Label(handleRect, "///");

            var current = Event.current;
            if (current == null)
            {
                return windowRect;
            }

            if (current.type == EventType.MouseDown && handleRect.Contains(current.mousePosition))
            {
                _resizingWindowId = windowId;
                _resizeStartRect = windowRect;
                _resizeStartMouse = current.mousePosition;
                current.Use();
            }
            else if (_resizingWindowId == windowId && current.type == EventType.MouseDrag)
            {
                var delta = current.mousePosition - _resizeStartMouse;
                windowRect.width = Mathf.Clamp(_resizeStartRect.width + delta.x, minWidth, maxWidth);
                windowRect.height = Mathf.Clamp(_resizeStartRect.height + delta.y, minHeight, maxHeight);
                current.Use();
            }
            else if (_resizingWindowId == windowId && (current.type == EventType.MouseUp || current.rawType == EventType.MouseUp))
            {
                _resizingWindowId = -1;
                current.Use();
            }

            return windowRect;
        }

        public static bool DrawCollapsedButton(Rect rect, string label, GUIStyle style)
        {
            return GUI.Button(rect, label, style);
        }

        public static void DrawActions(IList<CortexWindowAction> actions)
        {
            if (actions == null || actions.Count == 0)
            {
                return;
            }

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null || action.Execute == null)
                {
                    continue;
                }

                var width = action.Width > 0f ? action.Width : 80f;
                if (GUILayout.Button(action.Label ?? action.ActionId ?? "Action", GUILayout.Width(width)))
                {
                    action.Execute();
                }
            }
        }
    }
}
