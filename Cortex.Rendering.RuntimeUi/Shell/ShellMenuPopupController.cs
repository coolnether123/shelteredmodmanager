using System.Collections.Generic;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.Shell
{
    public struct ShellMenuPopupDismissResult
    {
        public bool ShouldClose;
        public bool ShouldConsumeInput;
    }

    public static class ShellMenuPopupController
    {
        public const float PopupWidth = 220f;
        public const float PopupMargin = 8f;
        public const float PopupVerticalPadding = 8f;
        public const float PopupItemHeight = 26f;
        public const float PopupVerticalOffset = 2f;

        public static RenderRect BuildPopupRect(RenderRect headerRect, RenderRect anchorRect, float windowWidth, int itemCount)
        {
            var popupHeight = (PopupVerticalPadding * 2f) + (itemCount * PopupItemHeight);
            var x = Clamp(headerRect.X + anchorRect.X, 0f, Max(0f, windowWidth - PopupWidth - PopupMargin));
            var y = headerRect.Y + anchorRect.Y + anchorRect.Height + PopupVerticalOffset;
            return new RenderRect(x, y, PopupWidth, popupHeight);
        }

        public static ShellMenuPopupDismissResult EvaluateDismissal(WorkbenchFrameInputSnapshot input, RenderRect headerRect, RenderRect popupRect, IList<RenderRect> groupRects)
        {
            var result = new ShellMenuPopupDismissResult();
            if (!input.HasCurrentEvent)
            {
                return result;
            }

            if (input.CurrentEventKind == WorkbenchInputEventKind.MouseDown)
            {
                var mousePosition = input.CurrentMousePosition;
                var overGroup = IsPointerOverMenuGroup(headerRect, groupRects, mousePosition);
                if (!RuntimeUiHitTest.Contains(popupRect, mousePosition) && !overGroup)
                {
                    result.ShouldClose = true;
                }
            }
            else if (input.CurrentEventKind == WorkbenchInputEventKind.KeyDown && input.CurrentKey == WorkbenchInputKey.Escape)
            {
                result.ShouldClose = true;
                result.ShouldConsumeInput = true;
            }

            return result;
        }

        private static bool IsPointerOverMenuGroup(RenderRect headerRect, IList<RenderRect> groupRects, RenderPoint pointerPosition)
        {
            if (groupRects == null)
            {
                return false;
            }

            for (var i = 0; i < groupRects.Count; i++)
            {
                var groupRect = groupRects[i];
                var translatedRect = new RenderRect(
                    headerRect.X + groupRect.X,
                    headerRect.Y + groupRect.Y,
                    groupRect.Width,
                    groupRect.Height);
                if (RuntimeUiHitTest.Contains(translatedRect, pointerPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static float Max(float left, float right)
        {
            return left > right ? left : right;
        }
    }
}
