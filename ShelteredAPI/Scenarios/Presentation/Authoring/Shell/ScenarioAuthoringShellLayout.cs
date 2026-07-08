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
        public const float TopBarHeight = 86f;
        public const float StatusHeight = 46f;
        public const float ToolRailWidth = 142f;
        public const float InspectorWidth = 300f;
        public const float InspectorMinWidth = 240f;
        public const float InspectorMaxWidth = 480f;
        public const float BottomTrayHeight = 286f;
        public const float BottomTrayCollapsedHeight = 44f;
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
        public const float FloatingWindowCascade = 24f;
        public const float CommandDockBottomOffset = 22f;
        public const float WorkshopPageInset = 8f;
        public const float WorkshopPageTopInset = 18f;
        public const float HomeWorkshopPageTopInset = 4f;

        // Top bar sizing. Reserves room on the left for the vanilla portrait and
        // on the right for the HUD so labels never collide with the game UI.
        public const float PortraitReserveWidth = 248f;
        public const float PortraitReserveHeight = 464f;
        public const float TopBarPreferredWidth = 1180f;
        public const float TopBarMinWidth = 560f;
        private const float MinToolRailButtonHeight = 24f;
        private const float ToolRailCompactGap = 4f;
        private const float ToolRailVerticalPadding = 24f;

        public static Rect BuildHudReserveRect(float scaledWidth)
        {
            float reserveWidth = Mathf.Clamp(HudReserveWidth, 280f, Math.Max(280f, scaledWidth * 0.36f));
            return new Rect(Math.Max(0f, scaledWidth - reserveWidth), 0f, reserveWidth, HudReserveHeight);
        }

        public static Rect BuildTopBarRect(float scaledWidth, Rect hudReserveRect)
        {
            bool compact = scaledWidth < 1400f;
            float leftBound = compact
                ? Mathf.Min(196f, Math.Max(0f, scaledWidth * 0.22f))
                : Mathf.Min(PortraitReserveWidth, Math.Max(0f, scaledWidth * 0.30f));
            float rightLimit = compact ? Math.Min(scaledWidth - Gutter, hudReserveRect.x + 80f) : hudReserveRect.x - Gutter;
            float rightBound = Math.Max(leftBound + TopBarMinWidth, rightLimit);
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
            return BuildInspectorRect(contentRect, InspectorWidth);
        }

        public static Rect BuildInspectorRect(Rect contentRect, float inspectorWidth)
        {
            // TODO(centralize): Right inspector layout is a remaining non-central panel.
            // Remove this once selection details move into the central workspace.
            // Anchor the inspector below the HUD reserve so it never sits on top of the
            // vanilla clock/magnifier widgets, regardless of how tall the top bar grows.
            float width = Mathf.Clamp(inspectorWidth > 0f ? inspectorWidth : InspectorWidth, InspectorMinWidth, InspectorMaxWidth);
            float minY = HudReserveHeight + Gutter + InspectorHudClearance;
            float y = Math.Max(contentRect.y + InspectorTopOffset, minY);
            float maxBottom = contentRect.yMax - Gutter;
            float height = Mathf.Clamp(maxBottom - y, 200f, 540f);
            float x = contentRect.xMax - width;
            return new Rect(x, y, width, height);
        }

        public static Rect BuildBottomTrayRect(Rect contentRect, float viewportLeft, float viewportRight)
        {
            // TODO(centralize): Bottom tray layout is still separate from the central workspace.
            // Retire this after placement tools are merged into the workspace page.
            float trayWidth = Math.Min(920f, Math.Max(560f, viewportRight - viewportLeft));
            float commandDockTop = contentRect.yMax - CommandDockHeight - CommandDockBottomOffset;
            float trayBottom = Math.Max(contentRect.y + 180f, commandDockTop - Gutter);
            float upperClearanceY = contentRect.y + 216f;
            float availableHeight = Math.Max(150f, trayBottom - upperClearanceY);
            float trayHeight = Mathf.Clamp(BottomTrayHeight, 246f, Math.Min(320f, availableHeight));
            float trayY = trayBottom - trayHeight;
            return new Rect(viewportLeft, trayY, trayWidth, trayHeight);
        }

        public static Rect BuildCollapsedBottomTrayRect(Rect contentRect, float viewportLeft, float viewportRight)
        {
            // TODO(centralize): Collapsed tray is a placement-mode holdover.
            // Replace with central workspace placement status when that surface exists.
            float trayWidth = Math.Min(760f, Math.Max(420f, viewportRight - viewportLeft));
            return new Rect(
                viewportLeft + Math.Max(0f, ((viewportRight - viewportLeft) - trayWidth) * 0.5f),
                contentRect.yMax - BottomTrayCollapsedHeight - CommandDockBottomOffset,
                trayWidth,
                BottomTrayCollapsedHeight);
        }

        public static Rect BuildPlaceflowBrowserRect(Rect viewportRect)
        {
            float margin = viewportRect.width >= 900f && viewportRect.height >= 620f ? Margin : 8f;
            return new Rect(
                viewportRect.x + margin,
                viewportRect.y + margin,
                Math.Max(320f, viewportRect.width - (margin * 2f)),
                Math.Max(220f, viewportRect.height - (margin * 2f)));
        }

        public static Rect BuildEmptyInspectorChipRect(Rect contentRect, float inspectorWidth)
        {
            // TODO(centralize): Empty inspector chip should disappear with the right inspector.
            float width = Mathf.Clamp(inspectorWidth > 0f ? inspectorWidth : InspectorWidth, InspectorMinWidth, InspectorMaxWidth);
            float chipWidth = Math.Min(width, 220f);
            return new Rect(contentRect.xMax - chipWidth, HudReserveHeight + Gutter + InspectorHudClearance, chipWidth, 34f);
        }

        public static Rect BuildWorkshopPageRect(Rect contentRect)
        {
            float horizontalInset = contentRect.width >= 760f ? WorkshopPageInset : 0f;
            float verticalInset = contentRect.height >= 300f ? WorkshopPageTopInset : Gutter;
            float pageWidth = Math.Max(240f, contentRect.width - (horizontalInset * 2f));
            return new Rect(
                contentRect.x + horizontalInset,
                contentRect.y + verticalInset,
                pageWidth,
                Math.Max(120f, contentRect.height - (verticalInset * 2f)));
        }

        public static Rect BuildHomeWorkshopPageRect(Rect contentRect)
        {
            float horizontalInset = contentRect.width >= 760f ? WorkshopPageInset : 0f;
            float verticalInset = contentRect.height >= 300f ? HomeWorkshopPageTopInset : 0f;
            float pageWidth = Math.Max(240f, contentRect.width - (horizontalInset * 2f));
            return new Rect(
                contentRect.x + horizontalInset,
                contentRect.y + verticalInset,
                pageWidth,
                Math.Max(120f, contentRect.height - (verticalInset * 2f)));
        }

        public static Rect BuildToolRailRect(Rect contentRect, int buttonCount)
        {
            return BuildToolRailRect(contentRect, buttonCount, 0f);
        }

        public static Rect BuildToolRailRect(Rect contentRect, int buttonCount, float bottomReserveHeight)
        {
            float portraitSafeY = Math.Max(contentRect.y + 26f, ResolvePortraitReserveHeight(contentRect));
            float availableHeight = Math.Max(112f, contentRect.yMax - portraitSafeY - Gutter - Math.Max(0f, bottomReserveHeight));
            float minimumButtonStack = ToolRailVerticalPadding
                + (Math.Max(0, buttonCount) * MinToolRailButtonHeight)
                + (Math.Max(0, buttonCount - 1) * ToolRailCompactGap);
            float regularHeight = 18f + (Math.Max(0, buttonCount) * 56f);
            float targetHeight = Math.Max(regularHeight, minimumButtonStack);
            float height = Math.Min(Math.Min(560f, regularHeight), availableHeight);
            if (buttonCount > 0 && height < minimumButtonStack)
                height = Math.Min(availableHeight, targetHeight);
            return new Rect(contentRect.x + 4f, portraitSafeY, ToolRailWidth, height);
        }

        public static Rect BuildRailRestoreChipRect(Rect contentRect, Rect railRect, int chipIndex, int chipCount)
        {
            const float chipHeight = 30f;
            const float chipGap = 6f;
            int safeIndex = Math.Max(0, chipIndex);
            float y = railRect.yMax + chipGap + (safeIndex * (chipHeight + chipGap));
            float lastAllowedY = contentRect.yMax - chipHeight - 4f;
            return new Rect(
                railRect.x,
                Math.Min(y, Math.Max(railRect.y, lastAllowedY - Math.Max(0, chipCount - safeIndex - 1) * (chipHeight + chipGap))),
                railRect.width,
                chipHeight);
        }

        private static float ResolvePortraitReserveHeight(Rect contentRect)
        {
            float scaledScreenHeight = contentRect.yMax + StatusHeight;
            if (scaledScreenHeight <= 760f)
                return 360f;
            if (scaledScreenHeight <= 820f)
                return Mathf.Lerp(360f, PortraitReserveHeight, (scaledScreenHeight - 760f) / 60f);

            return PortraitReserveHeight;
        }

        public static Rect BuildWorkspaceRect(Rect contentRect, bool reserveBottomTray)
        {
            return BuildWorkspaceRect(contentRect, reserveBottomTray, InspectorWidth);
        }

        public static Rect BuildWorkspaceRect(Rect contentRect, bool reserveBottomTray, float inspectorWidth)
        {
            Rect workspaceBounds = reserveBottomTray
                ? new Rect(contentRect.x, contentRect.y, contentRect.width, Math.Max(240f, contentRect.height - BottomTrayHeight - Gutter))
                : contentRect;
            float dockWidth = Mathf.Clamp(inspectorWidth > 0f ? inspectorWidth : InspectorWidth, InspectorMinWidth, InspectorMaxWidth);
            float maxWidth = Math.Max(320f, workspaceBounds.width - ((ToolRailWidth + Gutter + dockWidth + Gutter) * 0.5f));
            float maxHeight = Math.Max(260f, workspaceBounds.height - WorkspaceTabReserveHeight - Gutter);
            float width = Mathf.Clamp(workspaceBounds.width * 0.64f, Math.Min(680f, maxWidth), Math.Min(1040f, maxWidth));
            float height = Mathf.Clamp(workspaceBounds.height * 0.8f, Math.Min(460f, maxHeight), Math.Min(680f, maxHeight));
            float x = workspaceBounds.x + ((workspaceBounds.width - width) * 0.5f);
            float y = workspaceBounds.y + WorkspaceTabReserveHeight + 2f;
            float maxY = workspaceBounds.yMax - WorkspaceTabReserveHeight;
            if (y + height > maxY)
                y = Math.Max(workspaceBounds.y + WorkspaceTabReserveHeight, maxY - height);
            y = Math.Max(workspaceBounds.y + WorkspaceTabReserveHeight, y);
            return new Rect(x, y, width, height);
        }

        public static Rect BuildFloatingWindowRect(
            ScenarioAuthoringShellWindowViewModel window,
            Rect contentRect,
            int visibleFloatingIndex)
        {
            // TODO(centralize): Floating window positioning is still independent of the
            // workspace page. Remove when floating tools become central workspace regions.
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
            float y = defaultBounds.y + Math.Max(0f, (defaultBounds.height * 0.20f) - (height * 0.5f));
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
            return CascadeOffset(visibleFloatingIndex);
        }

        private static Vector2 CascadeOffset(int visibleFloatingIndex)
        {
            int slot = Math.Max(0, visibleFloatingIndex % 6);
            float offset = slot * FloatingWindowCascade;
            return new Vector2(offset, offset);
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

        public static Rect BuildCenteredPopupRect(float width, float height, float popupWidth, float popupHeight, Rect hudReserveRect)
        {
            Rect rect = new Rect(
                (width - popupWidth) * 0.5f,
                (height - popupHeight) * 0.5f,
                popupWidth,
                popupHeight);

            return ClampAwayFromHud(rect, width, height, hudReserveRect);
        }

    }
}
