using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cortex.Chrome
{
    internal sealed class CortexWindowAction
    {
        public string ActionId;
        public string Label;
        public string ToolTip;
        public float Width;
        public float Height;
        public Action Execute;
    }

    internal static class CortexWindowChromeController
    {
        private const float ResizeHandleSize = 18f;
        private static int _resizingWindowId = -1;
        private static Rect _resizeStartRect = new Rect(0f, 0f, 0f, 0f);
        private static Vector2 _resizeStartMouse = Vector2.zero;
        private static int _draggingSplitterId = -1;

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

        public static float DrawVerticalSplitter(int splitterId, float currentSize, float minSize, float maxSize, float thickness, bool invertDelta)
        {
            var rect = GUILayoutUtility.GetRect(thickness, 1f, GUILayout.Width(thickness), GUILayout.ExpandHeight(true));
            return DrawVerticalSplitter(splitterId, rect, currentSize, minSize, maxSize, invertDelta);
        }

        public static float DrawVerticalSplitter(int splitterId, Rect rect, float currentSize, float minSize, float maxSize, bool invertDelta)
        {
            GUI.Box(rect, string.Empty);
            var current = Event.current;
            if (current == null)
            {
                return currentSize;
            }

            if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition))
            {
                _draggingSplitterId = splitterId;
                current.Use();
            }
            else if (_draggingSplitterId == splitterId && current.type == EventType.MouseDrag)
            {
                currentSize = Mathf.Clamp(currentSize + (invertDelta ? -current.delta.x : current.delta.x), minSize, maxSize);
                current.Use();
            }
            else if (_draggingSplitterId == splitterId && (current.type == EventType.MouseUp || current.rawType == EventType.MouseUp))
            {
                _draggingSplitterId = -1;
                current.Use();
            }

            return currentSize;
        }

        public static float DrawHorizontalSplitter(int splitterId, float currentSize, float minSize, float maxSize, float thickness)
        {
            var rect = GUILayoutUtility.GetRect(1f, thickness, GUILayout.Height(thickness), GUILayout.ExpandWidth(true));
            return DrawHorizontalSplitter(splitterId, rect, currentSize, minSize, maxSize);
        }

        public static float DrawHorizontalSplitter(int splitterId, Rect rect, float currentSize, float minSize, float maxSize)
        {
            GUI.Box(rect, string.Empty);
            var current = Event.current;
            if (current == null)
            {
                return currentSize;
            }

            if (current.type == EventType.MouseDown && rect.Contains(current.mousePosition))
            {
                _draggingSplitterId = splitterId;
                current.Use();
            }
            else if (_draggingSplitterId == splitterId && current.type == EventType.MouseDrag)
            {
                currentSize = Mathf.Clamp(currentSize - current.delta.y, minSize, maxSize);
                current.Use();
            }
            else if (_draggingSplitterId == splitterId && (current.type == EventType.MouseUp || current.rawType == EventType.MouseUp))
            {
                _draggingSplitterId = -1;
                current.Use();
            }

            return currentSize;
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
                var height = action.Height > 0f ? action.Height : 22f;
                if (GUILayout.Button(new GUIContent(action.Label ?? action.ActionId ?? "Action", action.ToolTip ?? string.Empty), GUILayout.Width(width), GUILayout.Height(height)))
                {
                    action.Execute();
                }
            }
        }
    }
}
