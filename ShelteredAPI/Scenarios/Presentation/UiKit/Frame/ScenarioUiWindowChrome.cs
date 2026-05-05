using UnityEngine;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Layout;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.UiKit.Frame{
    /// <summary>
    /// Default scenario authoring window chrome. Paints the panel background,
    /// header strip with title/subtitle, and optionally a footer strip.
    /// Returns a <see cref="ScenarioUiWindowRegions"/> for the caller to fill.
    /// Stateless and safe to share across windows on the same style sheet.
    /// </summary>
    internal sealed class ScenarioUiWindowChrome : IScenarioUiWindowFrame
    {
        private readonly ScenarioUiStyleSheet _styles;

        public ScenarioUiWindowChrome(ScenarioUiStyleSheet styles)
        {
            if (styles == null)
                throw new System.ArgumentNullException("styles");
            _styles = styles;
        }

        public ScenarioUiWindowRegions Build(Rect outer, string title, string subtitle, bool reserveFooter)
        {
            return Build(outer, title, subtitle, reserveFooter, _styles.Theme.Metrics.HeaderHeight, 0f);
        }

        public ScenarioUiWindowRegions Build(
            Rect outer,
            string title,
            string subtitle,
            bool reserveFooter,
            float headerHeight,
            float titleRightInset)
        {
            IScenarioUiMetrics metrics = _styles.Theme.Metrics;

            // Paint the outer panel surface.
            GUI.Box(outer, GUIContent.none, _styles.PanelBase);

            Rect inner = ScenarioUiLayoutEngine.Inset(outer, metrics.CornerInset);

            Rect header;
            Rect afterHeader;
            ScenarioUiLayoutEngine.SplitTop(inner, headerHeight, metrics.PaddingXs, out header, out afterHeader);

            Rect body = afterHeader;
            Rect footer = new Rect(0f, 0f, 0f, 0f);
            if (reserveFooter)
                ScenarioUiLayoutEngine.SplitBottom(afterHeader, metrics.FooterHeight, metrics.PaddingXs, out body, out footer);

            DrawHeader(header, title, subtitle, metrics, titleRightInset);
            if (reserveFooter)
                GUI.Box(footer, GUIContent.none, _styles.Footer);

            ScenarioUiWindowRegions regions;
            regions.Outer = outer;
            regions.Header = header;
            regions.Body = body;
            regions.Footer = footer;
            return regions;
        }

        private void DrawHeader(Rect rect, string title, string subtitle, IScenarioUiMetrics metrics, float titleRightInset)
        {
            GUI.Box(rect, GUIContent.none, _styles.Header);
            Rect inner = ScenarioUiLayoutEngine.Inset(
                rect,
                metrics.HeaderPaddingX,
                0f,
                metrics.HeaderPaddingX + Mathf.Max(0f, titleRightInset),
                0f);

            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            if (hasSubtitle)
            {
                Rect titleRect;
                Rect subtitleRect;
                ScenarioUiLayoutEngine.SplitTop(
                    inner,
                    metrics.HeaderHeight * 0.55f,
                    0f,
                    out titleRect,
                    out subtitleRect);

                GUI.Label(titleRect, title ?? string.Empty, _styles.TitleText);
                GUI.Label(subtitleRect, subtitle, _styles.SubtitleText);
            }
            else
            {
                GUI.Label(inner, title ?? string.Empty, _styles.TitleText);
            }
        }
    }
}
