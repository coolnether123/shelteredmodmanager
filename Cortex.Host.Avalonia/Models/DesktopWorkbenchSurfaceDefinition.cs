namespace Cortex.Host.Avalonia.Models
{
    internal sealed class DesktopWorkbenchSurfaceDefinition
    {
        public string SurfaceId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string DefaultGroupId { get; set; } = string.Empty;
        public bool IsDocument { get; set; }
        public bool DefaultVisible { get; set; } = true;
        public bool IsRequired { get; set; }
    }
}
