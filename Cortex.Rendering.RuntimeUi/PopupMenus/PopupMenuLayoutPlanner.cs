using System.Collections.Generic;
using System.Text;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.PopupMenus
{
    public sealed class PopupMenuLayoutPlan
    {
        public RenderRect MenuRect;
        public float ContentHeight;
        public float MaxScroll;
    }

    public sealed class PopupMenuItemLayout
    {
        public PopupMenuItemModel Item;
        public RenderRect Bounds;
        public RenderRect LabelRect;
        public RenderRect ShortcutRect;
        public RenderRect SeparatorRect;
    }

    public sealed class PopupMenuScrollChromeLayout
    {
        public RenderRect ChromeRect;
        public RenderRect UpButtonRect;
        public RenderRect DownButtonRect;
        public RenderRect TrackRect;
        public RenderRect ThumbRect;
    }

    public sealed class PopupMenuDrawLayout
    {
        public RenderRect MenuRect;
        public RenderRect HeaderTextRect;
        public RenderRect ViewportRect;
        public float ContentWidth;
        public float ContentHeight;
        public float ScrollOffset;
        public float MaxScroll;
        public bool HasScroll;
        public readonly List<PopupMenuItemLayout> Items = new List<PopupMenuItemLayout>();
        public PopupMenuScrollChromeLayout ScrollChrome;
    }

    public static class PopupMenuLayoutPlanner
    {
        public const float HeaderHeight = 28f;
        public const float ItemHeight = 24f;
        public const float SeparatorHeight = 8f;
        public const float ScrollButtonHeight = 18f;
        public const float ScrollChromeWidth = 18f;
        public const float HorizontalPadding = 8f;
        public const float VerticalPadding = 6f;
        public const float ItemInset = 6f;
        public const float ItemLabelInset = 8f;
        public const float ItemShortcutWidth = 70f;
        public const float ItemShortcutInset = 78f;
        public const float ScrollThumbMinHeight = 24f;
        public const float DefaultMenuWidth = 280f;
        public const float ViewportMargin = 6f;

        public static PopupMenuLayoutPlan BuildLayout(RenderPoint position, RenderSize viewportSize, IList<PopupMenuItemModel> items)
        {
            var safeItems = items ?? new PopupMenuItemModel[0];
            var itemCount = 0;
            var separatorCount = 0;
            for (var i = 0; i < safeItems.Count; i++)
            {
                var item = safeItems[i];
                if (item == null)
                {
                    continue;
                }

                if (item.IsSeparator)
                {
                    separatorCount++;
                }
                else
                {
                    itemCount++;
                }
            }

            var contentHeight = (itemCount * (ItemHeight + 2f)) + (separatorCount * SeparatorHeight) + VerticalPadding;
            var maxHeight = Max(HeaderHeight + ItemHeight + VerticalPadding, viewportSize.Height - 12f);
            var height = Min(maxHeight, HeaderHeight + contentHeight);
            var x = Min(position.X, Max(ViewportMargin, viewportSize.Width - DefaultMenuWidth - ViewportMargin));
            var y = Min(position.Y, Max(ViewportMargin, viewportSize.Height - height - ViewportMargin));
            var plan = new PopupMenuLayoutPlan();
            plan.ContentHeight = contentHeight;
            plan.MaxScroll = Max(0f, contentHeight - Max(0f, height - HeaderHeight - VerticalPadding));
            plan.MenuRect = new RenderRect(x, y, DefaultMenuWidth, height);
            return plan;
        }

        public static PopupMenuDrawLayout BuildDrawLayout(RenderPoint position, RenderSize viewportSize, IList<PopupMenuItemModel> items, float scrollOffset, string headerText)
        {
            var plan = BuildLayout(position, viewportSize, items);
            var drawLayout = new PopupMenuDrawLayout();
            drawLayout.MenuRect = plan.MenuRect;
            drawLayout.ContentHeight = plan.ContentHeight;
            drawLayout.MaxScroll = plan.MaxScroll;
            drawLayout.ScrollOffset = ClampScrollOffset(scrollOffset, plan.MaxScroll);
            drawLayout.HasScroll = plan.MaxScroll > 0f;
            drawLayout.HeaderTextRect = new RenderRect(
                plan.MenuRect.X + HorizontalPadding,
                plan.MenuRect.Y + VerticalPadding,
                plan.MenuRect.Width - (HorizontalPadding * 2f),
                18f);
            drawLayout.ViewportRect = new RenderRect(
                plan.MenuRect.X + 4f,
                plan.MenuRect.Y + HeaderHeight,
                Max(0f, plan.MenuRect.Width - 8f),
                Max(0f, plan.MenuRect.Height - HeaderHeight - VerticalPadding));
            drawLayout.ContentWidth = Max(0f, drawLayout.ViewportRect.Width - (drawLayout.HasScroll ? ScrollChromeWidth : 0f));

            var y = -drawLayout.ScrollOffset;
            var safeItems = items ?? new PopupMenuItemModel[0];
            for (var i = 0; i < safeItems.Count; i++)
            {
                var item = safeItems[i];
                if (item == null)
                {
                    continue;
                }

                var itemLayout = new PopupMenuItemLayout();
                itemLayout.Item = item;
                if (item.IsSeparator)
                {
                    itemLayout.SeparatorRect = new RenderRect(HorizontalPadding, y + 3f, drawLayout.ContentWidth - (HorizontalPadding * 2f), 1f);
                    y += SeparatorHeight;
                }
                else if (item.IsSectionHeader)
                {
                    itemLayout.LabelRect = new RenderRect(10f, y + 3f, drawLayout.ContentWidth - 20f, 18f);
                    y += ItemHeight;
                }
                else
                {
                    itemLayout.Bounds = new RenderRect(ItemInset, y, drawLayout.ContentWidth - 12f, ItemHeight);
                    itemLayout.LabelRect = new RenderRect(
                        itemLayout.Bounds.X + ItemLabelInset,
                        itemLayout.Bounds.Y + 3f,
                        itemLayout.Bounds.Width - ItemShortcutWidth,
                        18f);
                    itemLayout.ShortcutRect = new RenderRect(
                        itemLayout.Bounds.X + itemLayout.Bounds.Width - ItemShortcutInset,
                        itemLayout.Bounds.Y + 3f,
                        ItemShortcutWidth,
                        18f);
                    y += ItemHeight + 2f;
                }

                drawLayout.Items.Add(itemLayout);
            }

            if (drawLayout.HasScroll)
            {
                drawLayout.ScrollChrome = BuildScrollChromeLayout(drawLayout.ViewportRect, drawLayout.ContentWidth, drawLayout.MaxScroll, drawLayout.ScrollOffset);
            }

            return drawLayout;
        }

        public static float ClampScrollOffset(float scrollOffset, float maxScroll)
        {
            return Clamp(scrollOffset, 0f, Max(0f, maxScroll));
        }

        public static string BuildMenuKey(string headerText, IList<PopupMenuItemModel> items)
        {
            var builder = new StringBuilder(headerText ?? string.Empty);
            if (items == null)
            {
                return builder.ToString();
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                builder
                    .Append('|')
                    .Append(item.CommandId ?? string.Empty)
                    .Append(':')
                    .Append(item.Label ?? string.Empty)
                    .Append(':')
                    .Append(item.IsSeparator ? '1' : '0')
                    .Append(':')
                    .Append(item.IsSectionHeader ? '1' : '0');
            }

            return builder.ToString();
        }

        public static PopupMenuScrollChromeLayout BuildScrollChromeLayout(RenderRect viewportRect, float contentWidth, float maxScroll, float scrollOffset)
        {
            var layout = new PopupMenuScrollChromeLayout();
            layout.ChromeRect = new RenderRect(contentWidth, 0f, ScrollChromeWidth, viewportRect.Height);
            layout.UpButtonRect = new RenderRect(layout.ChromeRect.X, layout.ChromeRect.Y, layout.ChromeRect.Width, ScrollButtonHeight);
            layout.DownButtonRect = new RenderRect(layout.ChromeRect.X, layout.ChromeRect.Y + layout.ChromeRect.Height - ScrollButtonHeight, layout.ChromeRect.Width, ScrollButtonHeight);
            layout.TrackRect = new RenderRect(
                layout.ChromeRect.X,
                layout.UpButtonRect.Y + layout.UpButtonRect.Height,
                layout.ChromeRect.Width,
                Max(0f, layout.ChromeRect.Height - (ScrollButtonHeight * 2f)));
            var thumbHeight = Max(
                ScrollThumbMinHeight,
                layout.TrackRect.Height * Clamp01(viewportRect.Height / Max(viewportRect.Height, viewportRect.Height + maxScroll)));
            var thumbTravel = Max(0f, layout.TrackRect.Height - thumbHeight);
            var thumbY = thumbTravel <= 0f
                ? layout.TrackRect.Y
                : layout.TrackRect.Y + ((ClampScrollOffset(scrollOffset, maxScroll) / Max(1f, maxScroll)) * thumbTravel);
            layout.ThumbRect = new RenderRect(layout.TrackRect.X + 2f, thumbY, Max(0f, layout.TrackRect.Width - 4f), thumbHeight);
            return layout;
        }

        private static float Min(float left, float right)
        {
            return left < right ? left : right;
        }

        private static float Max(float left, float right)
        {
            return left > right ? left : right;
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

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }
    }
}
