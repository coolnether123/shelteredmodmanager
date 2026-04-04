using System.Collections.Generic;

namespace Cortex.Host.Avalonia.Models
{
    internal sealed class DesktopShellState
    {
        public bool UseSavedLayout { get; set; }
        public string LastRuntimeLayoutPresetId { get; set; } = string.Empty;
        public string ActiveSurfaceId { get; set; } = string.Empty;
        public List<DesktopShellSurfaceState> SurfaceStates { get; set; } = new List<DesktopShellSurfaceState>();
    }

    internal sealed class DesktopShellSurfaceState
    {
        public string SurfaceId { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
    }
}
