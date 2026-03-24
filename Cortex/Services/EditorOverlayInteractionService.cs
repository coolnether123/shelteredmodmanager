using System;
using UnityEngine;

namespace Cortex.Services
{
    internal sealed class EditorOverlayInteractionService
    {
        private string _lastScrollOwnerKey = string.Empty;

        public bool IsPointerWithin(Rect rect, Vector2 pointer)
        {
            return rect.width > 0f &&
                rect.height > 0f &&
                rect.Contains(pointer);
        }

        public bool ShouldBypassSurfaceInput(Event current, bool pointerOnMethodInspector, bool pointerOnContextMenu)
        {
            if (current == null)
            {
                return false;
            }

            if (!IsMouseLikeEvent(current.type))
            {
                return false;
            }

            return pointerOnMethodInspector || pointerOnContextMenu;
        }

        public bool ShouldCloseMethodInspectorOnPointerDown(
            Event current,
            bool pointerOnMethodInspector,
            bool pointerOnContextMenu,
            CortexShellState state)
        {
            return current != null &&
                current.type == EventType.MouseDown &&
                !pointerOnMethodInspector &&
                !pointerOnContextMenu &&
                state != null &&
                state.Editor != null &&
                state.Editor.MethodInspector != null &&
                state.Editor.MethodInspector.IsVisible;
        }

        public bool ShouldPreserveEditorScroll(Event current, bool contextMenuOpen, bool pointerOnMethodInspector)
        {
            if (!contextMenuOpen && !pointerOnMethodInspector)
            {
                return false;
            }

            if (current != null && current.type == EventType.ScrollWheel)
            {
                return true;
            }

            return Mathf.Abs(Input.GetAxisRaw("Mouse ScrollWheel")) > 0.0001f ||
                Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.0001f;
        }

        public void TracePointerRouting(
            string surfaceName,
            Event current,
            bool pointerOnMethodInspector,
            bool pointerOnContextMenu,
            bool pointerWithinSurface,
            bool methodInspectorVisible)
        {
            if (current == null || current.type != EventType.MouseDown)
            {
                return;
            }

            var route = pointerOnMethodInspector
                ? "method-inspector"
                : pointerOnContextMenu
                    ? "context-menu"
                    : pointerWithinSurface
                        ? "editor-surface"
                        : "outside-surface";
            var closesInspector = methodInspectorVisible &&
                !pointerOnMethodInspector &&
                !pointerOnContextMenu;

            MMLog.WriteInfo("[Cortex.Overlay] Pointer down. Surface='" +
                (surfaceName ?? string.Empty) +
                "', Button=" + current.button +
                ", Route='" + route +
                "', PointerWithinSurface=" + pointerWithinSurface +
                ", MethodInspectorVisible=" + methodInspectorVisible +
                ", ClosesMethodInspector=" + closesInspector + ".");
        }

        public void TraceScrollOwner(string surfaceName, Event current, bool pointerOnMethodInspector, bool pointerOnContextMenu)
        {
            if (current == null || current.type != EventType.ScrollWheel)
            {
                return;
            }

            var owner = pointerOnMethodInspector
                ? "method-inspector"
                : pointerOnContextMenu
                    ? "context-menu"
                    : "editor-surface";
            var ownerKey = (surfaceName ?? string.Empty) + "|" + owner;
            if (string.Equals(_lastScrollOwnerKey, ownerKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastScrollOwnerKey = ownerKey;
            MMLog.WriteInfo("[Cortex.Overlay] Scroll owner changed. Surface='" +
                (surfaceName ?? string.Empty) +
                "', Owner='" + owner +
                "', PointerOnMethodInspector=" + pointerOnMethodInspector +
                ", PointerOnContextMenu=" + pointerOnContextMenu + ".");
        }

        private static bool IsMouseLikeEvent(EventType type)
        {
            return type == EventType.MouseDown ||
                type == EventType.MouseUp ||
                type == EventType.MouseDrag ||
                type == EventType.ScrollWheel ||
                type == EventType.ContextClick;
        }
    }
}
