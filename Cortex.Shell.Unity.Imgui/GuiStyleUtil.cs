using UnityEngine;

namespace Cortex
{
    internal static class GuiStyleUtil
    {
        public static void ApplyBackgroundToAllStates(GUIStyle style, Texture2D background)
        {
            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
            style.onNormal.background = background;
            style.onHover.background = background;
            style.onActive.background = background;
            style.onFocused.background = background;
        }

        public static void ApplyTextColorToAllStates(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }
    }
}
