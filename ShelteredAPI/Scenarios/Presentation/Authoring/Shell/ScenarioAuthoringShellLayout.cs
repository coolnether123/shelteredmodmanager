using System;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    /// <summary>
    /// Pure layout calculations for the IMGUI authoring shell. Owns the spatial
    /// constants and Rect math so the render module only handles drawing.
    /// </summary>
    internal static class ScenarioAuthoringShellLayout
    {
        public const float Margin = 16f;
        public const float Gutter = 12f;
        public const float TopBarHeight = 96f;
        public const float StatusHeight = 46f;
        public const float ToolRailWidth = 74f;
        public const float InspectorWidth = 316f;
        public const float BottomTrayHeight = 272f;
        public const float CommandDockHeight = 48f;

        // Reserve area for the vanilla HUD (clock, magnifier, resource readouts) in the
        // top-right of the screen. Tuned so the inspector and popups never sit on top of it.
        public const float HudReserveWidth = 340f;
        public const float HudReserveHeight = 172f;

        // Extra breathing room between the HUD reserve and the inspector header.
        public const float InspectorHudClearance = 16f;
        public const float InspectorTopOffset = 30f;
        public const float WorkspaceTabReserveHeight = 42f;
        public const float FloatingWindowStartY = 46f;
        public const float FloatingWindowCascade = 28f;

        // Top bar sizing. Reserves room on the left for the vanilla portrait and
        // on the right for the HUD so labels never collide with the game UI.
        public const float PortraitReserveWidth = 248f;
        public const float TopBarPreferredWidth = 1180f;
        public const float TopBarMinWidth = 560f;

        public static Rect BuildHudReserveRect(float scaledWidth)
        {
            float reserveWidth = Mathf.Clamp(HudReserveWidth, 280f, Math.Max(280f, scaledWidth * 0.36f));
            return new Rect(Math.Max(0f, scaledWidth - reserveWidth), 0f, reserveWidth, HudReserveHeight);
        }

        public static Rect BuildTopBarRect(float scaledWidth, Rect hudReserveRect)
        {
            float leftBound = Mathf.Min(PortraitReserveWidth, Math.Max(0f, scaledWidth * 0.30f));
            float rightBound = Math.Max(leftBound + TopBarMinWidth, hudReserveRect.x - Gutter);
            float availableWidth = Math.Max(TopBarMinWidth, rightBound - leftBound);
            float width = Math.Min(TopBarPreferredWidth, availableWidth);
            float x = leftBound + ((availableWidth - width) * 0.5f);
            return new Rect(x, 0f, width, TopBarHeight);
        }

        public static Rect BuildStatusRect(float scaledWidth, float scaledHeight)
        {
            return new Rect(0f, scaledHeight - StatusHeight, scaledWidth, StatusHeight);
        }

        public static Rect BuildContentRect(float scaledWidth, Rect topRect, Rect statusRect)
        {
            return new Rect(
                Margin,
                topRect.yMax + Gutter,
                scaledWidth - (Margin * 2f),
                statusRect.y - (topRect.yMax + Gutter));
        }

        public static Rect BuildInspectorRect(Rect contentRect)
        {
            // Anchor the inspector below the HUD reserve so it never sits on top of the
            // vanilla clock/magnifier widgets, regardless of how tall the top bar grows.
            float minY = HudReserveHeight + Gutter + InspectorHudClearance;
            float y = Math.Max(contentRect.y + InspectorTopOffset, minY);
            float maxBottom = contentRect.yMax - Gutter;
            float height = Mathf.Clamp(maxBottom - y, 200f, 540f);
            float x = contentRect.xMax - InspectorWidth;
            return new Rect(x, y, InspectorWidth, height);
        }

        public static Rect BuildBottomTrayRect(Rect contentRect, float viewportLeft, float viewportRight)
        {
            float trayWidth = Math.Min(940f, Math.Max(520f, viewportRight - viewportLeft));
            float trayY = Math.Max(contentRect.y + 220f, contentRect.yMax - BottomTrayHeight);
            return new Rect(viewportLeft, trayY, trayWidth, BottomTrayHeight);
        }

        public static Rect BuildWorkspaceRect(Rect contentRect, bool reserveBottomTray)
        {
            Rect workspaceBounds = reserveBottomTray
                ? new Rect(contentRect.x, contentRect.y, contentRect.width, Math.Max(240f, contentRect.height - BottomTrayHeight - Gutter))
                : contentRect;
            float maxWidth = Math.Max(320f, workspaceBounds.width - ((ToolRailWidth + Gutter + InspectorWidth + Gutter) * 0.5f));
            float maxHeight = Math.Max(220f, workspaceBounds.height - WorkspaceTabReserveHeight - Gutter);
            float width = Mathf.Clamp(workspaceBounds.width * 0.58f, Math.Min(640f, maxWidth), Math.Min(980f, maxWidth));
            float height = Mathf.Clamp(workspaceBounds.height * 0.72f, Math.Min(400f, maxHeight), Math.Min(620f, maxHeight));
            float x = workspaceBounds.x + ((workspaceBounds.width - width) * 0.5f);
            float y = workspaceBounds.y + ((workspaceBounds.height - height) * 0.5f) + (WorkspaceTabReserveHeight * 0.5f);
            y = Math.Max(workspaceBounds.y + WorkspaceTabReserveHeight, y);
            return new Rect(x, y, width, height);
        }

        public static Rect BuildFloatingWindowRect(
            ScenarioAuthoringShellWindowViewModel window,
            Rect contentRect,
            int visibleFloatingIndex)
        {
            float minWidth = window != null && window.MinWidth > 0f ? window.MinWidth : 260f;
            float minHeight = window != null && window.MinHeight > 0f ? window.MinHeight : 140f;
            float width = window != null && window.Width > 0f ? window.Width : minWidth;
            float height = window != null && window.Height > 0f ? window.Height : minHeight;

            Rect rect;
            if (window != null && window.HasCustomBounds)
            {
                rect = new Rect(window.X, window.Y, width, height);
            }
            else
            {
                rect = BuildDefaultFloatingWindowRect(window, contentRect, visibleFloatingIndex, width, height);
            }

            return ClampWindowRect(rect, contentRect, minWidth, minHeight);
        }

        private static Rect BuildDefaultFloatingWindowRect(
            ScenarioAuthoringShellWindowViewModel window,
            Rect contentRect,
            int visibleFloatingIndex,
            float width,
            float height)
        {
            Rect defaultBounds = BuildDefaultWindowBounds(contentRect);
            float x = defaultBounds.x + ((defaultBounds.width - width) * 0.5f);
            float y = defaultBounds.y + ((defaultBounds.height - height) * 0.5f);
            Vector2 offset = ResolveDefaultWindowOffset(window, contentRect, visibleFloatingIndex);
            return new Rect(x + offset.x, y + offset.y, width, height);
        }

        private static Rect BuildDefaultWindowBounds(Rect contentRect)
        {
            float x = contentRect.x + ToolRailWidth + Gutter;
            float y = contentRect.y + WorkspaceTabReserveHeight;
            float width = Math.Max(320f, contentRect.xMax - x - Gutter);
            float height = Math.Max(240f, contentRect.yMax - y - CommandDockHeight - Gutter);
            return new Rect(x, y, width, height);
        }

        private static Vector2 ResolveDefaultWindowOffset(
            ScenarioAuthoringShellWindowViewModel window,
            Rect contentRect,
            int visibleFloatingIndex)
        {
            if (window == null || string.IsNullOrEmpty(window.Id))
                return CascadeOffset(visibleFloatingIndex);

            float horizontal = Math.Min(280f, contentRect.width * 0.18f);
            float vertical = Math.Min(170f, contentRect.height * 0.18f);

            if (window.WorkspaceStage != ScenarioStageKind.None)
                return Vector2.zero;

            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase))
                return new Vector2(horizontal, -18f);
            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase))
                return new Vector2(0f, vertical);
            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Calendar, StringComparison.OrdinalIgnoreCase))
                return new Vector2(0f, -vertical * 0.35f);
            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Hierarchy, StringComparison.OrdinalIgnoreCase))
                return new Vector2(-horizontal, -vertical * 0.45f);
            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.SelectionStack, StringComparison.OrdinalIgnoreCase))
                return new Vector2(-horizontal, vertical * 0.55f);
            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase))
                return new Vector2(-horizontal * 0.5f, -vertical * 0.5f);
            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Settings, StringComparison.OrdinalIgnoreCase))
                return new Vector2(0f, 0f);

            return CascadeOffset(visibleFloatingIndex);
        }

        private static Vector2 CascadeOffset(int visibleFloatingIndex)
        {
            int slot = Math.Max(0, visibleFloatingIndex % 5);
            float offset = (slot - 2) * FloatingWindowCascade;
            return new Vector2(offset, offset * 0.75f);
        }

        public static Rect ClampWindowRect(Rect rect, Rect bounds, float minWidth, float minHeight)
        {
            float safeMinWidth = Math.Max(120f, minWidth);
            float safeMinHeight = Math.Max(80f, minHeight);
            float maxWidth = Math.Max(safeMinWidth, bounds.width - (Margin * 2f));
            float maxHeight = Math.Max(safeMinHeight, bounds.height - (Margin * 2f));
            float width = Mathf.Clamp(rect.width, safeMinWidth, maxWidth);
            float height = Mathf.Clamp(rect.height, safeMinHeight, maxHeight);
            float minX = bounds.x + Margin;
            float minY = bounds.y + Margin;
            float maxX = Math.Max(minX, bounds.xMax - width - Margin);
            float maxY = Math.Max(minY, bounds.yMax - height - Margin);

            return new Rect(
                Mathf.Clamp(rect.x, minX, maxX),
                Mathf.Clamp(rect.y, minY, maxY),
                width,
                height);
        }

        /// <summary>
        /// Pushes the given rect into the visible area and away from the vanilla HUD reserve
        /// region in the top-right. Used for popups, menus, and tooltips.
        /// </summary>
        public static Rect ClampAwayFromHud(Rect rect, float width, float height, Rect hudReserveRect)
        {
            Rect clamped = new Rect(
                Mathf.Clamp(rect.x, Margin, width - rect.width - Margin),
                Mathf.Clamp(rect.y, Margin, height - rect.height - Margin),
                rect.width,
                rect.height);

            if (clamped.Overlaps(hudReserveRect))
            {
                float shiftedLeft = hudReserveRect.x - clamped.width - Gutter;
                if (shiftedLeft >= Margin)
                    clamped.x = shiftedLeft;
                else
                    clamped.y = hudReserveRect.yMax + Gutter;
            }

            clamped.x = Mathf.Clamp(clamped.x, Margin, width - clamped.width - Margin);
            clamped.y = Mathf.Clamp(clamped.y, Margin, height - clamped.height - Margin);
            return clamped;
        }

    }
}
