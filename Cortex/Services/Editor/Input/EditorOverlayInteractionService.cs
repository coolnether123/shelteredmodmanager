using System;
using UnityEngine;

namespace Cortex.Services.Editor.Input
{
    internal sealed class EditorOverlayPointerState
    {
        public bool PointerWithinSurface;
        public bool PointerOnMethodInspector;
        public bool PointerOnContextMenu;
        public bool PointerOnHoverSurface;
        public bool PointerOnOverlaySurface;
        public string PointerRoute = string.Empty;
        public string ScrollOwner = string.Empty;
    }

    internal sealed class EditorOverlayInteractionService
    {
        private string _lastScrollOwnerKey = string.Empty;

        public bool IsPointerWithin(Rect rect, Vector2 pointer)
        {
            return rect.width > 0f &&
                rect.height > 0f &&
                rect.Contains(pointer);
        }

        public EditorOverlayPointerState ResolvePointerState(
            Rect methodInspectorRect,
            Rect contextMenuRect,
            bool contextMenuOpen,
            bool pointerOnHoverSurface,
            bool pointerWithinSurface,
            Vector2 pointer)
        {
            var state = new EditorOverlayPointerState();
            state.PointerWithinSurface = pointerWithinSurface;
            state.PointerOnHoverSurface = pointerOnHoverSurface;
            state.PointerOnMethodInspector = IsPointerWithin(methodInspectorRect, pointer);
            state.PointerOnContextMenu = contextMenuOpen && IsPointerWithin(contextMenuRect, pointer);
            state.PointerOnOverlaySurface = state.PointerOnMethodInspector || state.PointerOnHoverSurface;
            state.PointerRoute = state.PointerOnMethodInspector
                ? "method-inspector"
                : state.PointerOnHoverSurface
                    ? "hover-surface"
                    : state.PointerOnContextMenu
                    ? "context-menu"
                    : state.PointerWithinSurface
                        ? "editor-surface"
                        : "outside-surface";
            state.ScrollOwner = state.PointerOnOverlaySurface
                ? (state.PointerOnMethodInspector ? "method-inspector" : "hover-surface")
                : state.PointerOnContextMenu
                    ? "context-menu"
                    : "editor-surface";
            return state;
        }

        public bool ShouldBypassSurfaceInput(Event current, EditorOverlayPointerState pointerState)
        {
            return ShouldBypassSurfaceInput(current != null ? current.type : EventType.Ignore, pointerState);
        }

        public bool ShouldBypassSurfaceInput(EventType currentType, EditorOverlayPointerState pointerState)
        {
            if (!IsMouseLikeEvent(currentType))
            {
                return false;
            }

            return pointerState != null &&
                (pointerState.PointerOnMethodInspector || pointerState.PointerOnContextMenu || pointerState.PointerOnHoverSurface);
        }

        public bool ShouldCloseMethodInspectorOnPointerDown(
            Event current,
            EditorOverlayPointerState pointerState,
            CortexShellState state)
        {
            return ShouldCloseMethodInspectorOnPointerDown(
                current != null ? current.type : EventType.Ignore,
                pointerState,
                state);
        }

        public bool ShouldCloseMethodInspectorOnPointerDown(
            EventType currentType,
            EditorOverlayPointerState pointerState,
            CortexShellState state)
        {
            return currentType == EventType.MouseDown &&
                (pointerState == null || !pointerState.PointerOnMethodInspector) &&
                (pointerState == null || !pointerState.PointerOnContextMenu) &&
                (pointerState == null || !pointerState.PointerOnHoverSurface) &&
                state != null &&
                state.Editor != null &&
                state.Editor.MethodInspector != null &&
                state.Editor.MethodInspector.IsVisible;
        }

        public bool ShouldPreserveEditorScroll(Event current, bool contextMenuOpen, EditorOverlayPointerState pointerState)
        {
            if (!contextMenuOpen &&
                (pointerState == null || !pointerState.PointerOnOverlaySurface))
            {
                return false;
            }

            if (current != null && current.type == EventType.ScrollWheel)
            {
                return true;
            }

            return Mathf.Abs(UnityEngine.Input.GetAxisRaw("Mouse ScrollWheel")) > 0.0001f ||
                Mathf.Abs(UnityEngine.Input.GetAxis("Mouse ScrollWheel")) > 0.0001f;
        }

        public void TracePointerRouting(
            string surfaceName,
            Event current,
            EditorOverlayPointerState pointerState,
            bool methodInspectorVisible)
        {
            if (current == null || current.type != EventType.MouseDown)
            {
                return;
            }

            var closesInspector = methodInspectorVisible &&
                (pointerState == null || !pointerState.PointerOnMethodInspector) &&
                (pointerState == null || !pointerState.PointerOnContextMenu) &&
                (pointerState == null || !pointerState.PointerOnHoverSurface);

            MMLog.WriteInfo("[Cortex.Overlay] Pointer down. Surface='" +
                (surfaceName ?? string.Empty) +
                "', Button=" + current.button +
                ", Route='" + (pointerState != null ? pointerState.PointerRoute ?? string.Empty : string.Empty) +
                "', PointerWithinSurface=" + (pointerState != null && pointerState.PointerWithinSurface) +
                ", MethodInspectorVisible=" + methodInspectorVisible +
                ", ClosesMethodInspector=" + closesInspector + ".");
        }

        public void TraceScrollOwner(string surfaceName, Event current, EditorOverlayPointerState pointerState)
        {
            if (current == null || current.type != EventType.ScrollWheel)
            {
                return;
            }

            var owner = pointerState != null ? pointerState.ScrollOwner ?? string.Empty : "editor-surface";
            var ownerKey = (surfaceName ?? string.Empty) + "|" + owner;
            if (string.Equals(_lastScrollOwnerKey, ownerKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastScrollOwnerKey = ownerKey;
            MMLog.WriteInfo("[Cortex.Overlay] Scroll owner changed. Surface='" +
                (surfaceName ?? string.Empty) +
                "', Owner='" + owner +
                "', PointerOnMethodInspector=" + (pointerState != null && pointerState.PointerOnMethodInspector) +
                ", PointerOnContextMenu=" + (pointerState != null && pointerState.PointerOnContextMenu) +
                ", PointerOnHoverSurface=" + (pointerState != null && pointerState.PointerOnHoverSurface) + ".");
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
