using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Widgets
{
    /// <summary>
    /// Shared split-page row treatment used by book-style selection lists.
    /// </summary>
    internal static class BookSelectionRowStyle
    {
        public static Color Background(bool locked)
        {
            return locked
                ? new Color(0.72f, 0.50f, 0.46f, 0.44f)
                : new Color(0.92f, 0.84f, 0.66f, 0.32f);
        }

        public static Color HoverBackground(bool locked)
        {
            return locked
                ? new Color(0.78f, 0.53f, 0.48f, 0.62f)
                : new Color(1f, 0.91f, 0.68f, 0.54f);
        }

        public static Color TitleColor(IThemePalette palette, bool locked)
        {
            if (palette == null)
                return Color.black;

            return locked ? palette.StampRed : palette.Ink;
        }

        public static void BuildSplitPageBackground(
            GameObject root,
            UIPrimitiveFactory ui,
            ITextureLibrary textures,
            int leftPageX,
            int rightPageX,
            int leftPageWidth,
            int rightPageWidth,
            int height,
            bool locked,
            out UITexture leftBackground,
            out UITexture rightBackground)
        {
            leftBackground = null;
            rightBackground = null;
            if (root == null || ui == null || textures == null)
                return;

            Color background = Background(locked);
            leftBackground = ui.CreateQuad(root, "LeftPageBackground", textures.White,
                new Vector3(leftPageX, 0f, 0f), leftPageWidth, height, background, ui.NextDepth());
            rightBackground = ui.CreateQuad(root, "RightPageBackground", textures.White,
                new Vector3(rightPageX, 0f, 0f), rightPageWidth, height, background, ui.NextDepth());
        }

        public static HoverVisualState AttachSplitPageHover(
            GameObject root,
            UITexture leftBackground,
            UITexture rightBackground,
            UIWidget[] widgets,
            Color[] restColors,
            Color[] hoverColors,
            bool locked,
            float hoverScale)
        {
            if (root == null)
                return null;

            HoverVisualState state = root.AddComponent<HoverVisualState>();
            state.Widgets = PrependBackgrounds(leftBackground, rightBackground, widgets);
            state.RestColors = PrependBackgroundColors(Background(locked), restColors);
            state.HoverColors = PrependBackgroundColors(HoverBackground(locked), hoverColors);
            state.ScaleTarget = root.transform;
            state.RestScale = 1f;
            state.HoverScale = hoverScale;
            return state;
        }

        private static UIWidget[] PrependBackgrounds(UIWidget leftBackground, UIWidget rightBackground, UIWidget[] widgets)
        {
            int extra = widgets != null ? widgets.Length : 0;
            UIWidget[] result = new UIWidget[2 + extra];
            result[0] = leftBackground;
            result[1] = rightBackground;
            for (int i = 0; i < extra; i++)
                result[i + 2] = widgets[i];
            return result;
        }

        private static Color[] PrependBackgroundColors(Color background, Color[] colors)
        {
            int extra = colors != null ? colors.Length : 0;
            Color[] result = new Color[2 + extra];
            result[0] = background;
            result[1] = background;
            for (int i = 0; i < extra; i++)
                result[i + 2] = colors[i];
            return result;
        }
    }
}
