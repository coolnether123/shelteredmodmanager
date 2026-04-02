using System.Collections.Generic;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.Panels
{
    public interface IPanelLayoutMeasurer
    {
        float MeasureMetadataHeight(float width, PanelMetadataElement element);
        float MeasureTextHeight(float width, PanelTextElement element);
        float MeasureActionHeight(float width, PanelActionElement element);
        float MeasureCardHeight(float width, PanelCardElement element);
    }

    public sealed class PanelRootLayout
    {
        public RenderRect PanelRect;
        public RenderRect HeaderRect;
        public RenderRect HeaderActionsRect;
        public RenderRect ContentViewport;
        public RenderRect TitleRect;
        public RenderRect SubtitleRect;
        public RenderRect CloseButtonRect;
        public readonly List<RenderRect> HeaderActionButtonRects = new List<RenderRect>();
    }

    public sealed class PanelContentLayout
    {
        public readonly List<PanelSectionLayout> Sections = new List<PanelSectionLayout>();
        public float TotalHeight;
    }

    public sealed class PanelSectionLayout
    {
        public PanelSection Section;
        public RenderRect HeaderRect;
        public readonly List<PanelElementLayout> ElementLayouts = new List<PanelElementLayout>();
    }

    public sealed class PanelElementLayout
    {
        public readonly PanelElement Element;
        public readonly RenderRect Bounds;
        public readonly float NextY;
        public PanelMetadataContentLayout MetadataLayout;
        public PanelTextContentLayout TextLayout;
        public PanelActionElementContentLayout ActionLayout;
        public PanelCardContentLayout CardLayout;

        public PanelElementLayout(PanelElement element, RenderRect bounds, float nextY)
        {
            Element = element;
            Bounds = bounds;
            NextY = nextY;
        }
    }

    public sealed class PanelCardContentLayout
    {
        public RenderRect TitleRect;
        public bool HasBody;
        public RenderRect BodyRect;
        public readonly List<PanelMetadataElement> Rows = new List<PanelMetadataElement>();
        public readonly List<PanelMetadataContentLayout> RowLayouts = new List<PanelMetadataContentLayout>();
        public readonly List<PanelAction> Actions = new List<PanelAction>();
        public readonly List<RenderRect> ActionRects = new List<RenderRect>();
    }

    public sealed class PanelMetadataContentLayout
    {
        public RenderRect KeyRect;
        public RenderRect ValueRect;
        public RenderRect DividerRect;
    }

    public sealed class PanelTextContentLayout
    {
        public bool HasLabel;
        public RenderRect LabelRect;
        public RenderRect ValueRect;
    }

    public sealed class PanelActionElementContentLayout
    {
        public RenderRect ButtonRect;
        public bool HasHint;
        public RenderRect HintRect;
    }

    public static class PanelLayoutPlanner
    {
        public const float HeaderHeight = 54f;
        public const float ActionStripHeight = 30f;
        public const float Padding = 10f;
        public const float SectionSpacing = 6f;
        public const float ExpandedSectionTopSpacing = 4f;
        public const float CardPadding = 8f;
        public const float DividerHeight = 1f;
        public const float LabelWidth = 96f;
        public const float SectionHeaderHeight = 26f;
        public const float HeaderActionButtonHeight = 24f;
        public const float HeaderActionButtonSpacing = 6f;
        public const float CardActionButtonHeight = 24f;
        public const float CardActionButtonSpacing = 4f;
        public const float MinContentWidth = 120f;
        public const float MinContentHeight = 40f;

        public static PanelRootLayout BuildRootLayout(RenderRect panelRect, PanelAction[] headerActions)
        {
            var hasHeaderActions = headerActions != null && headerActions.Length > 0;
            var headerRect = new RenderRect(0f, 0f, panelRect.Width, HeaderHeight);
            var actionsRect = hasHeaderActions
                ? new RenderRect(Padding, HeaderHeight + 6f, panelRect.Width - (Padding * 2f), ActionStripHeight)
                : new RenderRect(Padding, HeaderHeight + 6f, panelRect.Width - (Padding * 2f), 0f);
            var contentTop = hasHeaderActions ? actionsRect.Y + actionsRect.Height + 8f : headerRect.Y + headerRect.Height + 10f;
            var layout = new PanelRootLayout();
            layout.PanelRect = new RenderRect(0f, 0f, panelRect.Width, panelRect.Height);
            layout.HeaderRect = headerRect;
            layout.HeaderActionsRect = actionsRect;
            layout.ContentViewport = new RenderRect(
                Padding,
                contentTop,
                panelRect.Width - (Padding * 2f),
                Max(24f, panelRect.Height - contentTop - Padding));
            layout.TitleRect = new RenderRect(Padding, 8f, headerRect.Width - 62f, 24f);
            layout.SubtitleRect = new RenderRect(Padding, 31f, headerRect.Width - 62f, 18f);
            layout.CloseButtonRect = new RenderRect(headerRect.Width - 42f, 12f, 28f, 26f);
            BuildHeaderActionButtonRects(layout, headerActions);
            return layout;
        }

        public static PanelContentLayout BuildContentLayout(PanelDocument document, float width, IPanelLayoutMeasurer measurer)
        {
            var layout = new PanelContentLayout();
            var y = 0f;
            var sections = document != null && document.Sections != null ? document.Sections : new PanelSection[0];
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var sectionLayout = new PanelSectionLayout();
                sectionLayout.Section = section;
                sectionLayout.HeaderRect = new RenderRect(0f, y, width, SectionHeaderHeight);
                layout.Sections.Add(sectionLayout);

                y += SectionHeaderHeight;
                if (!section.Expanded)
                {
                    y += SectionSpacing;
                    continue;
                }

                y += ExpandedSectionTopSpacing;
                var elements = section.Elements ?? new PanelElement[0];
                for (var elementIndex = 0; elementIndex < elements.Length; elementIndex++)
                {
                    var element = elements[elementIndex];
                    if (element == null)
                    {
                        continue;
                    }

                    var elementLayout = BuildElementLayout(width, y, element, measurer);
                    y = elementLayout.NextY;
                    sectionLayout.ElementLayouts.Add(elementLayout);
                }

                y += SectionSpacing;
            }

            layout.TotalHeight = Max(MinContentHeight, y);
            return layout;
        }

        public static PanelCardContentLayout BuildCardContentLayout(RenderRect cardRect, PanelCardElement element, IPanelLayoutMeasurer measurer)
        {
            var layout = new PanelCardContentLayout();
            var innerWidth = Max(40f, cardRect.Width - (CardPadding * 2f));
            var x = cardRect.X + CardPadding;
            var y = cardRect.Y + CardPadding;

            if (!string.IsNullOrEmpty(element != null ? element.Title : string.Empty))
            {
                layout.TitleRect = new RenderRect(x, y, innerWidth, 18f);
                y += 20f;
            }

            var rows = element != null && element.Rows != null ? element.Rows : new PanelMetadataElement[0];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    continue;
                }

                var rowHeight = measurer != null ? measurer.MeasureMetadataHeight(innerWidth, row) : 18f;
                layout.Rows.Add(row);
                layout.RowLayouts.Add(BuildMetadataContentLayout(new RenderRect(x, y, innerWidth, rowHeight)));
                y += rowHeight + 3f + (row.DrawDivider ? DividerHeight + 2f : 0f);
            }

            if (!string.IsNullOrEmpty(element != null ? element.Body : string.Empty))
            {
                layout.HasBody = true;
                layout.BodyRect = new RenderRect(x, y, innerWidth, Max(18f, cardRect.Height - (y - cardRect.Y) - CardPadding));
                y += layout.BodyRect.Height + CardActionButtonSpacing;
            }

            var actions = element != null && element.Actions != null ? element.Actions : new PanelAction[0];
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                layout.Actions.Add(action);
                layout.ActionRects.Add(new RenderRect(x, y, Min(180f, innerWidth), CardActionButtonHeight));
                y += CardActionButtonHeight + CardActionButtonSpacing;
            }

            return layout;
        }

        public static PanelMetadataContentLayout BuildMetadataContentLayout(RenderRect bounds)
        {
            var layout = new PanelMetadataContentLayout();
            layout.KeyRect = new RenderRect(bounds.X, bounds.Y, LabelWidth, bounds.Height);
            layout.ValueRect = new RenderRect(bounds.X + LabelWidth, bounds.Y, Max(40f, bounds.Width - LabelWidth), bounds.Height);
            layout.DividerRect = new RenderRect(bounds.X, bounds.Y + bounds.Height + 3f, bounds.Width, DividerHeight);
            return layout;
        }

        public static PanelTextContentLayout BuildTextContentLayout(RenderRect bounds, PanelTextElement element, IPanelLayoutMeasurer measurer)
        {
            var layout = new PanelTextContentLayout();
            var y = bounds.Y;
            if (!string.IsNullOrEmpty(element != null ? element.Label : string.Empty))
            {
                layout.HasLabel = true;
                layout.LabelRect = new RenderRect(bounds.X, y, bounds.Width, 18f);
                y += 20f;
            }

            var valueHeight = Max(18f, bounds.Height - (y - bounds.Y));
            layout.ValueRect = new RenderRect(bounds.X, y, bounds.Width, valueHeight);
            return layout;
        }

        public static PanelActionElementContentLayout BuildActionElementContentLayout(RenderRect bounds, PanelActionElement element, IPanelLayoutMeasurer measurer)
        {
            var layout = new PanelActionElementContentLayout();
            layout.ButtonRect = new RenderRect(
                bounds.X + CardPadding,
                bounds.Y + CardPadding,
                bounds.Width - (CardPadding * 2f),
                CardActionButtonHeight);
            if (!string.IsNullOrEmpty(element != null ? element.Hint : string.Empty))
            {
                layout.HasHint = true;
                layout.HintRect = new RenderRect(
                    layout.ButtonRect.X,
                    layout.ButtonRect.Y + layout.ButtonRect.Height + CardActionButtonSpacing,
                    layout.ButtonRect.Width,
                    Max(18f, bounds.Height - ((layout.ButtonRect.Y + layout.ButtonRect.Height + CardActionButtonSpacing) - bounds.Y) - CardPadding));
            }

            return layout;
        }

        private static PanelElementLayout BuildElementLayout(float width, float startY, PanelElement element, IPanelLayoutMeasurer measurer)
        {
            var bounds = new RenderRect(0f, startY, width, 0f);
            switch (element.Kind)
            {
                case PanelElementKind.Metadata:
                    var metadata = element as PanelMetadataElement;
                    bounds.Height = measurer != null ? measurer.MeasureMetadataHeight(width, metadata) : 18f;
                    var metadataLayout = new PanelElementLayout(element, bounds, startY + bounds.Height + 3f + (metadata != null && metadata.DrawDivider ? DividerHeight + 2f : 0f));
                    metadataLayout.MetadataLayout = BuildMetadataContentLayout(bounds);
                    return metadataLayout;
                case PanelElementKind.Text:
                    bounds.Height = measurer != null ? measurer.MeasureTextHeight(width, element as PanelTextElement) : 18f;
                    var textLayout = new PanelElementLayout(element, bounds, startY + bounds.Height);
                    textLayout.TextLayout = BuildTextContentLayout(bounds, element as PanelTextElement, measurer);
                    return textLayout;
                case PanelElementKind.Action:
                    bounds.Height = measurer != null ? measurer.MeasureActionHeight(width, element as PanelActionElement) : 34f;
                    var actionLayout = new PanelElementLayout(element, bounds, startY + bounds.Height + 4f);
                    actionLayout.ActionLayout = BuildActionElementContentLayout(bounds, element as PanelActionElement, measurer);
                    return actionLayout;
                case PanelElementKind.Card:
                    bounds.Height = measurer != null ? measurer.MeasureCardHeight(width, element as PanelCardElement) : 34f;
                    var cardLayout = new PanelElementLayout(element, bounds, startY + bounds.Height + 4f);
                    cardLayout.CardLayout = BuildCardContentLayout(bounds, element as PanelCardElement, measurer);
                    return cardLayout;
                case PanelElementKind.Spacer:
                    var spacer = element as PanelSpacerElement;
                    bounds.Height = spacer != null ? Max(0f, spacer.Height) : 0f;
                    return new PanelElementLayout(element, bounds, startY + bounds.Height);
                default:
                    return new PanelElementLayout(element, bounds, startY);
            }
        }

        private static void BuildHeaderActionButtonRects(PanelRootLayout layout, PanelAction[] actions)
        {
            if (layout == null || actions == null || actions.Length == 0 || layout.HeaderActionsRect.Height <= 0f)
            {
                return;
            }

            var buttonWidth = Max(92f, Min(132f, (layout.HeaderActionsRect.Width - 18f) / Max(1f, actions.Length)));
            var x = layout.HeaderActionsRect.X;
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    layout.HeaderActionButtonRects.Add(new RenderRect(x, layout.HeaderActionsRect.Y, buttonWidth, HeaderActionButtonHeight));
                    x += buttonWidth + HeaderActionButtonSpacing;
                    continue;
                }

                layout.HeaderActionButtonRects.Add(new RenderRect(x, layout.HeaderActionsRect.Y, buttonWidth, HeaderActionButtonHeight));
                x += buttonWidth + HeaderActionButtonSpacing;
            }
        }

        private static float Min(float left, float right)
        {
            return left < right ? left : right;
        }

        private static float Max(float left, float right)
        {
            return left > right ? left : right;
        }
    }
}
