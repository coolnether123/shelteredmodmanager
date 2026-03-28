namespace Cortex.Presentation.Models
{
    public enum MethodInspectorElementKind
    {
        Metadata,
        Text,
        Action,
        Card,
        Spacer
    }

    public sealed class MethodInspectorViewModel
    {
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        public bool ShowCloseButton = true;
        public MethodInspectorActionViewModel[] HeaderActions = new MethodInspectorActionViewModel[0];
        public MethodInspectorSectionViewModel[] Sections = new MethodInspectorSectionViewModel[0];
    }

    public sealed class MethodInspectorActionViewModel
    {
        public string Id = string.Empty;
        public string Label = string.Empty;
        public string Hint = string.Empty;
        public bool Enabled = true;
        public bool Emphasized;
    }

    public sealed class MethodInspectorSectionViewModel
    {
        public string Id = string.Empty;
        public string Title = string.Empty;
        public bool Expanded = true;
        public MethodInspectorElementViewModel[] Elements = new MethodInspectorElementViewModel[0];
    }

    public abstract class MethodInspectorElementViewModel
    {
        public MethodInspectorElementKind Kind;
    }

    public sealed class MethodInspectorMetadataViewModel : MethodInspectorElementViewModel
    {
        public string Label = string.Empty;
        public string Value = string.Empty;
        public bool DrawDivider = true;

        public MethodInspectorMetadataViewModel()
        {
            Kind = MethodInspectorElementKind.Metadata;
        }
    }

    public sealed class MethodInspectorTextViewModel : MethodInspectorElementViewModel
    {
        public string Label = string.Empty;
        public string Value = string.Empty;
        public bool Monospace;

        public MethodInspectorTextViewModel()
        {
            Kind = MethodInspectorElementKind.Text;
        }
    }

    public sealed class MethodInspectorActionElementViewModel : MethodInspectorElementViewModel
    {
        public MethodInspectorActionViewModel Action = new MethodInspectorActionViewModel();
        public string Hint = string.Empty;

        public MethodInspectorActionElementViewModel()
        {
            Kind = MethodInspectorElementKind.Action;
        }
    }

    public sealed class MethodInspectorCardViewModel : MethodInspectorElementViewModel
    {
        public string Title = string.Empty;
        public string Body = string.Empty;
        public MethodInspectorMetadataViewModel[] Rows = new MethodInspectorMetadataViewModel[0];
        public MethodInspectorActionViewModel[] Actions = new MethodInspectorActionViewModel[0];

        public MethodInspectorCardViewModel()
        {
            Kind = MethodInspectorElementKind.Card;
        }
    }

    public sealed class MethodInspectorSpacerViewModel : MethodInspectorElementViewModel
    {
        public float Height = 6f;

        public MethodInspectorSpacerViewModel()
        {
            Kind = MethodInspectorElementKind.Spacer;
        }
    }
}
