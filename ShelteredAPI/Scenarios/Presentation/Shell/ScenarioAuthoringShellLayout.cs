using System;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
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

        // Top bar sizing. Reserves room on the left for the vanilla portrait and
        // on the right for the HUD so labels never collide with the game UI.
        public const float PortraitReserveWidth = 248f;
        public const float TopBarPreferredWidth = 1180f;
        public const float TopBarMinWidth = 560f;

        // Mode chip needs to comfortably fit a draft name on a single muted line.
        public const float ModeChipWidth = 290f;
        public const float ModeChipHeight = 44f;

        public const int DraftLabelMaxChars = 22;

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

        public static Rect BuildWorkspaceRect(Rect contentRect)
        {
            float width = Mathf.Clamp(contentRect.width * 0.58f, 640f, 980f);
            float height = Mathf.Clamp(contentRect.height * 0.72f, 400f, 620f);
            float x = contentRect.x + ((contentRect.width - width) * 0.5f);
            float y = contentRect.y + ((contentRect.height - height) * 0.5f);
            return new Rect(x, y, width, height);
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

        public static string TruncateDraftLabel(string draft)
        {
            if (string.IsNullOrEmpty(draft))
                return "Untitled";

            if (draft.Length <= DraftLabelMaxChars)
                return draft;

            return draft.Substring(0, DraftLabelMaxChars - 1) + "...";
        }
    }
}
