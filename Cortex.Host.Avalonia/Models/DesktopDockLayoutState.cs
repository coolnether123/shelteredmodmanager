using System.Collections.Generic;

namespace Cortex.Host.Avalonia.Models
{
    internal sealed class DesktopDockLayoutState
    {
        public int Version { get; set; } = 1;
        public List<DesktopDockGroupState> Groups { get; set; } = new List<DesktopDockGroupState>();
    }

    internal sealed class DesktopDockGroupState
    {
        public string GroupId { get; set; } = string.Empty;
        public double Proportion { get; set; } = 1.0;
        public string ActiveSurfaceId { get; set; } = string.Empty;
        public List<string> SurfaceIds { get; set; } = new List<string>();
    }
}
