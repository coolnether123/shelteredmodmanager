namespace Cortex.Rendering.Models
{
    public struct RenderPoint
    {
        public float X;
        public float Y;

        public RenderPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static RenderPoint Zero
        {
            get { return new RenderPoint(0f, 0f); }
        }
    }

    public struct RenderRect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public RenderRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public struct RenderSize
    {
        public float Width;
        public float Height;

        public RenderSize(float width, float height)
        {
            Width = width;
            Height = height;
        }
    }

    public struct RenderColor
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public RenderColor(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }

    public sealed class PanelThemePalette
    {
        public string ThemeKey = string.Empty;
        public RenderColor BackgroundColor;
        public RenderColor HeaderColor;
        public RenderColor BorderColor;
        public RenderColor DividerColor;
        public RenderColor ActionFillColor;
        public RenderColor ActionActiveFillColor;
        public RenderColor CardFillColor;
        public RenderColor TextColor;
        public RenderColor MutedTextColor;
        public RenderColor AccentColor;
        public RenderColor WarningColor;
    }

    public sealed class PanelRenderResult
    {
        public RenderPoint Scroll = RenderPoint.Zero;
        public string ActivatedId = string.Empty;
    }

    public sealed class PopupMenuItemModel
    {
        public string CommandId = string.Empty;
        public string Label = string.Empty;
        public string ShortcutText = string.Empty;
        public bool Enabled;
        public bool IsSeparator;
        public bool IsSectionHeader;
    }

    public sealed class PopupMenuThemePalette
    {
        public RenderColor BackgroundColor;
        public RenderColor BorderColor;
        public RenderColor TextColor;
        public RenderColor MutedTextColor;
        public RenderColor AccentColor;
        public RenderColor HoverFillColor;
        public RenderColor PressedFillColor;
    }

    public struct PopupMenuRenderResult
    {
        public bool ShouldClose;
        public string ActivatedCommandId;
        public RenderRect MenuRect;
    }

    public sealed class HoverTooltipPartModel
    {
        public string Text = string.Empty;
        public bool IsInteractive;
        public object Tag;
    }

    public sealed class HoverTooltipSectionModel
    {
        public string Title = string.Empty;
        public string Text = string.Empty;
    }

    public sealed class HoverTooltipThemePalette
    {
        public RenderColor BackgroundColor;
        public RenderColor BorderColor;
        public RenderColor TextColor;
        public RenderColor MutedTextColor;
        public RenderColor AccentColor;
        public RenderColor HoverFillColor;
    }

    public sealed class HoverTooltipRenderModel
    {
        public string Key = string.Empty;
        public string DocumentPath = string.Empty;
        public RenderRect AnchorRect = new RenderRect(0f, 0f, 0f, 0f);
        public string QualifiedPath = string.Empty;
        public string SymbolDisplay = string.Empty;
        public HoverTooltipPartModel[] SignatureParts;
        public System.Func<HoverTooltipPartModel, string> BuildMetaText;
        public System.Func<HoverTooltipPartModel, string> BuildDocumentationText;
        public System.Func<HoverTooltipPartModel, HoverTooltipSectionModel[]> GetSupplementalSections;
        public object Tag;
    }

    public sealed class HoverTooltipRenderResult
    {
        public bool Visible;
        public HoverTooltipRenderModel Model;
        public HoverTooltipPartModel HoveredPart;
        public HoverTooltipPartModel ActivatedPart;
        public RenderRect TooltipRect = new RenderRect(0f, 0f, 0f, 0f);
    }
}
