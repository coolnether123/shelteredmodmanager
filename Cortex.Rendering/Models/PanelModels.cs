using System;

namespace Cortex.Rendering.Models
{
    public static class PanelCommandIds
    {
        public const string Close = "close";
        private const string SectionTogglePrefix = "section:";

        public static string CreateSectionToggle(string sectionId)
        {
            return SectionTogglePrefix + (sectionId ?? string.Empty);
        }

        public static bool TryGetSectionToggle(string commandId, out string sectionId)
        {
            sectionId = string.Empty;
            if (string.IsNullOrEmpty(commandId) || !commandId.StartsWith(SectionTogglePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            sectionId = commandId.Substring(SectionTogglePrefix.Length);
            return true;
        }
    }

    public enum PanelElementKind
    {
        Metadata,
        Text,
        Action,
        Card,
        Spacer
    }

    public sealed class PanelDocument
    {
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        public bool ShowCloseButton = true;
        public PanelAction[] HeaderActions = new PanelAction[0];
        public PanelSection[] Sections = new PanelSection[0];
    }

    public sealed class PanelAction
    {
        public string Id = string.Empty;
        public string Label = string.Empty;
        public string Hint = string.Empty;
        public bool Enabled = true;
        public bool Emphasized;
    }

    public sealed class PanelSection
    {
        public string Id = string.Empty;
        public string Title = string.Empty;
        public bool Expanded = true;
        public PanelElement[] Elements = new PanelElement[0];
    }

    public abstract class PanelElement
    {
        public PanelElementKind Kind;
    }

    public sealed class PanelMetadataElement : PanelElement
    {
        public string Label = string.Empty;
        public string Value = string.Empty;
        public bool DrawDivider = true;

        public PanelMetadataElement()
        {
            Kind = PanelElementKind.Metadata;
        }
    }

    public sealed class PanelTextElement : PanelElement
    {
        public string Label = string.Empty;
        public string Value = string.Empty;
        public bool Monospace;

        public PanelTextElement()
        {
            Kind = PanelElementKind.Text;
        }
    }

    public sealed class PanelActionElement : PanelElement
    {
        public PanelAction Action = new PanelAction();
        public string Hint = string.Empty;

        public PanelActionElement()
        {
            Kind = PanelElementKind.Action;
        }
    }

    public sealed class PanelCardElement : PanelElement
    {
        public string Title = string.Empty;
        public string Body = string.Empty;
        public PanelMetadataElement[] Rows = new PanelMetadataElement[0];
        public PanelAction[] Actions = new PanelAction[0];

        public PanelCardElement()
        {
            Kind = PanelElementKind.Card;
        }
    }

    public sealed class PanelSpacerElement : PanelElement
    {
        public float Height = 6f;

        public PanelSpacerElement()
        {
            Kind = PanelElementKind.Spacer;
        }
    }
}
