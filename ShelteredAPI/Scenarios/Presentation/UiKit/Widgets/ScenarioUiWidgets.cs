using UnityEngine;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Layout;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
namespace ShelteredAPI.Scenarios.Presentation.UiKit.Widgets{
    /// <summary>
    /// Reusable IMGUI draw helpers for scenario authoring windows. Stateless
    /// static methods that take a <see cref="ScenarioUiStyleSheet"/> and a rect.
    /// Each helper draws and returns the body region the caller can keep
    /// filling. Composing widgets is just chaining rect math.
    /// </summary>
    internal static class ScenarioUiWidgets
    {
        /// <summary>
        /// Paints a card surface with an optional title strip. Returns the
        /// inner content rect (already inset by the card's padding) for the
        /// caller to render into.
        /// </summary>
        public static Rect DrawCard(Rect rect, ScenarioUiStyleSheet styles, string title)
        {
            if (styles == null)
                return rect;

            IScenarioUiMetrics metrics = styles.Theme.Metrics;
            GUI.Box(rect, GUIContent.none, styles.Card);

            Rect inner = ScenarioUiLayoutEngine.Inset(rect, metrics.CardPadding);
            if (string.IsNullOrEmpty(title))
                return inner;

            Rect titleRect;
            Rect bodyRect;
            ScenarioUiLayoutEngine.SplitTop(inner, metrics.CardTitleHeight, metrics.PaddingXs, out titleRect, out bodyRect);
            GUI.Label(titleRect, title, styles.SectionTitleText);
            return bodyRect;
        }

        /// <summary>Draws a 1px (or themed) horizontal divider centered in the rect.</summary>
        public static void DrawHorizontalDivider(Rect rect, ScenarioUiStyleSheet styles)
        {
            if (styles == null)
                return;
            float thickness = styles.Theme.Metrics.DividerThickness;
            float y = rect.y + ((rect.height - thickness) * 0.5f);
            Rect line = new Rect(rect.x, y, rect.width, thickness);
            GUI.DrawTexture(line, styles.BorderSubtleTexture);
        }

        /// <summary>Vertical equivalent of <see cref="DrawHorizontalDivider"/>.</summary>
        public static void DrawVerticalDivider(Rect rect, ScenarioUiStyleSheet styles)
        {
            if (styles == null)
                return;
            float thickness = styles.Theme.Metrics.DividerThickness;
            float x = rect.x + ((rect.width - thickness) * 0.5f);
            Rect line = new Rect(x, rect.y, thickness, rect.height);
            GUI.DrawTexture(line, styles.BorderSubtleTexture);
        }

        /// <summary>
        /// Draws a small badge/pill. <paramref name="emphasis"/> selects the
        /// colour role; the label is rendered centred in the pill rect.
        /// </summary>
        public static void DrawPill(Rect rect, string label, ScenarioUiStyleSheet styles, ScenarioUiPillEmphasis emphasis)
        {
            if (styles == null)
                return;
            GUIStyle box;
            switch (emphasis)
            {
                case ScenarioUiPillEmphasis.Active:
                    box = styles.PillEmphasized;
                    break;
                case ScenarioUiPillEmphasis.Danger:
                    box = styles.PillDanger;
                    break;
                default:
                    box = styles.Pill;
                    break;
            }
            GUI.Box(rect, label ?? string.Empty, box);
        }

        /// <summary>
        /// Draws a key/value row split horizontally. The label uses muted
        /// styling and the value uses body styling. Useful for read-only fields
        /// in inspectors and summary cards.
        /// </summary>
        public static void DrawKeyValueRow(Rect rect, string label, string value, ScenarioUiStyleSheet styles)
        {
            if (styles == null)
                return;
            Rect[] cells = ScenarioUiLayoutEngine.Columns(rect, styles.Theme.Metrics.PaddingSm, 1f, 1.4f);
            if (cells.Length < 2)
                return;

            GUI.Label(cells[0], label ?? string.Empty, styles.MutedText);
            GUI.Label(cells[1], value ?? string.Empty, styles.BodyText);
        }

        /// <summary>
        /// Paints a centered empty-state message. Useful for windows whose
        /// content depends on a selection or draft that may not yet exist.
        /// </summary>
        public static void DrawEmptyState(Rect rect, string message, ScenarioUiStyleSheet styles)
        {
            if (styles == null)
                return;
            GUI.Label(rect, message ?? string.Empty, styles.EmptyStateText);
        }

        public static void DrawSpritePreviewFrame(Rect rect, Sprite sprite, ScenarioUiStyleSheet styles, bool emphasized)
        {
            if (styles == null)
                return;

            GUI.Box(rect, GUIContent.none, emphasized ? styles.ButtonActive : styles.Field);
            if (sprite == null || sprite.texture == null)
            {
                DrawEmptyState(rect, "No Sprite", styles);
                return;
            }

            Rect textureRect = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Rect uv = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);

            Rect fitted = FitRect(rect, textureRect.width, textureRect.height, styles.Theme.Metrics.PaddingXs);
            GUI.DrawTextureWithTexCoords(fitted, texture, uv, true);
        }

        /// <summary>
        /// Convenience for drawing a section title strip above a body. Returns
        /// the body rect with the title height already removed.
        /// </summary>
        public static Rect DrawSectionTitle(Rect rect, string title, ScenarioUiStyleSheet styles)
        {
            if (styles == null)
                return rect;
            IScenarioUiMetrics metrics = styles.Theme.Metrics;
            Rect titleRect;
            Rect bodyRect;
            ScenarioUiLayoutEngine.SplitTop(rect, metrics.CardTitleHeight, metrics.PaddingXs, out titleRect, out bodyRect);
            GUI.Label(titleRect, title ?? string.Empty, styles.SectionTitleText);
            return bodyRect;
        }

        private static Rect FitRect(Rect rect, float sourceWidth, float sourceHeight, float padding)
        {
            Rect inner = new Rect(rect.x + padding, rect.y + padding, rect.width - (padding * 2f), rect.height - (padding * 2f));
            if (sourceWidth <= 0f || sourceHeight <= 0f || inner.width <= 0f || inner.height <= 0f)
                return inner;

            float scale = Mathf.Min(inner.width / sourceWidth, inner.height / sourceHeight);
            float width = sourceWidth * scale;
            float height = sourceHeight * scale;
            return new Rect(
                inner.x + ((inner.width - width) * 0.5f),
                inner.y + ((inner.height - height) * 0.5f),
                width,
                height);
        }
    }

    /// <summary>Visual emphasis level for <see cref="ScenarioUiWidgets.DrawPill"/>.</summary>
    internal enum ScenarioUiPillEmphasis
    {
        Default = 0,
        Active = 1,
        Danger = 2
    }
}
