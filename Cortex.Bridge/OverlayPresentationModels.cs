using System.Collections.Generic;

namespace Cortex.Bridge
{
    public static class DesktopBridgeFeatureIds
    {
        public const string OverlayPresentation = "overlay.presentation";
        public const string OverlayInputIntents = "overlay.input-intents";
        public const string OverlayLifecycle = "overlay.lifecycle";
        public const string OverlayWindowStateSync = "overlay.window-state";
        public const string Heartbeat = "heartbeat";
    }

    public sealed class BridgeCapabilitySet
    {
        public List<string> Features { get; set; } = new List<string>();
    }

    public enum OverlayInputIntentType
    {
        None = 0,
        PointerEnter = 1,
        PointerLeave = 2,
        FocusSurface = 3,
        BlurSurface = 4,
        Escape = 5
    }

    public enum OverlayHostLifecycleKind
    {
        None = 0,
        Connected = 1,
        Heartbeat = 2,
        RequestLatestSnapshot = 3,
        ShutdownRequested = 4,
        ShutdownAcknowledged = 5,
        HostClosing = 6
    }

    public sealed class OverlayPresentationSnapshotMessage
    {
        public long Revision { get; set; }
        public OverlayPresentationSnapshot Snapshot { get; set; } = new OverlayPresentationSnapshot();
    }

    public sealed class OverlayInputIntentMessage
    {
        public string RequestId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public string TargetRegionId { get; set; } = string.Empty;
        public string TargetContainerId { get; set; } = string.Empty;
        public OverlayInputIntentType IntentType { get; set; }
    }

    public sealed class OverlayHostLifecycleMessage
    {
        public long Revision { get; set; }
        public OverlayHostLifecycleKind Kind { get; set; }
        public string LaunchToken { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public string UtcTimestamp { get; set; } = string.Empty;
    }

    public sealed class OverlayWindowStateChangedMessage
    {
        public long Revision { get; set; }
        public string SurfaceId { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public bool IsCollapsed { get; set; }
        public int ZOrder { get; set; }
        public OverlayRect Bounds { get; set; } = new OverlayRect();
    }

    public sealed class OverlayPresentationSnapshot
    {
        public long Revision { get; set; }
        public string PresentationModeId { get; set; } = string.Empty;
        public string ActiveSurfaceId { get; set; } = string.Empty;
        public string FocusedRegionId { get; set; } = string.Empty;
        public string RendererSummary { get; set; } = string.Empty;
        public OverlayGameWindowSnapshot GameWindow { get; set; } = new OverlayGameWindowSnapshot();
        public List<OverlaySurfaceSnapshot> Surfaces { get; set; } = new List<OverlaySurfaceSnapshot>();
    }

    public sealed class OverlayGameWindowSnapshot
    {
        public int ProcessId { get; set; }
        public string Title { get; set; } = string.Empty;
        public OverlayRect ClientBounds { get; set; } = new OverlayRect();
    }

    public sealed class OverlaySurfaceSnapshot
    {
        public string SurfaceId { get; set; } = string.Empty;
        public string HostLocationId { get; set; } = string.Empty;
        public string ContentViewId { get; set; } = string.Empty;
        public string ActiveContainerId { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public bool IsCollapsed { get; set; }
        public int ZOrder { get; set; }
        public OverlayRect Bounds { get; set; } = new OverlayRect();
        public OverlaySurfaceChrome Chrome { get; set; } = new OverlaySurfaceChrome();
        public List<OverlayHitRegion> HitRegions { get; set; } = new List<OverlayHitRegion>();
    }

    public sealed class OverlaySurfaceChrome
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public bool ShowCloseButton { get; set; } = true;
        public bool ShowCollapseButton { get; set; } = true;
    }

    public sealed class OverlayHitRegion
    {
        public string RegionId { get; set; } = string.Empty;
        public bool Interactive { get; set; }
        public OverlayRect Bounds { get; set; } = new OverlayRect();
    }

    public sealed class OverlayRect
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public sealed class OverlayRevisionTracker
    {
        private long _latestAcceptedRevision = -1;

        public long LatestAcceptedRevision
        {
            get { return _latestAcceptedRevision; }
        }

        public bool ShouldAccept(long revision)
        {
            if (revision < 0 || revision <= _latestAcceptedRevision)
            {
                return false;
            }

            _latestAcceptedRevision = revision;
            return true;
        }
    }
}
