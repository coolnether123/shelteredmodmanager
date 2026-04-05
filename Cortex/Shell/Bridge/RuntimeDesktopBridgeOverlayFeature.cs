using System;
using Cortex.Bridge;
using Cortex.Core.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Shell;

namespace Cortex.Shell.Bridge
{
    internal sealed class RuntimeDesktopBridgeOverlayFeature
    {
        private readonly CortexShellState _shellState;
        private readonly CortexShellViewState _viewState;
        private readonly Func<IWorkbenchRuntime> _runtimeAccessor;
        private OverlayPresentationSnapshot _overlaySnapshot = new OverlayPresentationSnapshot();
        private string _cachedFingerprint = string.Empty;

        public RuntimeDesktopBridgeOverlayFeature(
            CortexShellState shellState,
            CortexShellViewState viewState,
            Func<IWorkbenchRuntime> runtimeAccessor)
        {
            _shellState = shellState ?? new CortexShellState();
            _viewState = viewState ?? new CortexShellViewState();
            _runtimeAccessor = runtimeAccessor;
        }

        public long Revision
        {
            get { return _overlaySnapshot != null ? _overlaySnapshot.Revision : 0L; }
        }

        public void Initialize()
        {
            _overlaySnapshot = new OverlayPresentationSnapshot();
            _cachedFingerprint = BuildFingerprint(_overlaySnapshot);
        }

        public bool SynchronizeFromRuntime(OverlayPresentationSnapshot overlaySnapshot)
        {
            var candidate = overlaySnapshot ?? new OverlayPresentationSnapshot();
            var fingerprint = BuildFingerprint(candidate);
            if (string.Equals(_cachedFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return false;
            }

            _overlaySnapshot = candidate;
            _cachedFingerprint = fingerprint;
            return true;
        }

        public OverlayPresentationSnapshot BuildSnapshot()
        {
            return _overlaySnapshot ?? new OverlayPresentationSnapshot();
        }

        public bool TryApplyInputIntent(OverlayInputIntentMessage intent, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (intent == null)
            {
                return false;
            }

            var runtime = _runtimeAccessor != null ? _runtimeAccessor() : null;
            if (runtime != null && runtime.FocusState != null)
            {
                runtime.FocusState.FocusedRegionId = !string.IsNullOrEmpty(intent.TargetContainerId)
                    ? intent.TargetContainerId
                    : intent.SurfaceId ?? string.Empty;
            }

            if (intent.IntentType == OverlayInputIntentType.FocusSurface)
            {
                statusMessage = "Focused overlay surface " + (intent.SurfaceId ?? string.Empty) + ".";
            }
            else if (intent.IntentType == OverlayInputIntentType.PointerEnter)
            {
                statusMessage = "Overlay pointer entered " + (intent.SurfaceId ?? string.Empty) + ".";
            }
            else if (intent.IntentType == OverlayInputIntentType.PointerLeave)
            {
                statusMessage = "Overlay pointer left " + (intent.SurfaceId ?? string.Empty) + ".";
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                _shellState.StatusMessage = statusMessage;
            }

            return true;
        }

        public bool TryApplyWindowState(OverlayWindowStateChangedMessage message, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (message == null || string.IsNullOrEmpty(message.SurfaceId))
            {
                return false;
            }

            var target = ResolveWindowState(message.SurfaceId);
            if (target == null)
            {
                return false;
            }

            var bounds = message.Bounds ?? new OverlayRect();
            target.ExpandedRect = new Rendering.Models.RenderRect(
                (float)bounds.X,
                (float)bounds.Y,
                (float)bounds.Width,
                (float)bounds.Height);
            target.CollapsedRect = CortexShellWindowViewState.BuildCollapsedRect(
                target.ExpandedRect,
                target.CollapsedWidth,
                target.CollapsedHeight);
            target.IsCollapsed = message.IsCollapsed;
            target.CurrentRect = message.IsCollapsed ? target.CollapsedRect : target.ExpandedRect;

            statusMessage = "Overlay surface state synchronized for " + message.SurfaceId + ".";
            _shellState.StatusMessage = statusMessage;
            return true;
        }

        public bool TryApplyLifecycle(OverlayHostLifecycleMessage message, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (message == null)
            {
                return false;
            }

            if (message.Kind == OverlayHostLifecycleKind.ShutdownAcknowledged)
            {
                statusMessage = string.IsNullOrEmpty(message.StatusMessage)
                    ? "External overlay host acknowledged shutdown."
                    : message.StatusMessage;
                _shellState.StatusMessage = statusMessage;
                return true;
            }

            if (message.Kind == OverlayHostLifecycleKind.Connected)
            {
                statusMessage = string.IsNullOrEmpty(message.StatusMessage)
                    ? "External overlay host connected."
                    : message.StatusMessage;
                _shellState.StatusMessage = statusMessage;
                return true;
            }

            return message.Kind != OverlayHostLifecycleKind.None;
        }

        private CortexShellWindowViewState ResolveWindowState(string surfaceId)
        {
            if (string.Equals(surfaceId, OverlayPresentationSnapshotBuilder.ControlSurfaceId, StringComparison.OrdinalIgnoreCase))
            {
                return _viewState.OverlayControlWindow;
            }

            if (string.Equals(surfaceId, OverlayPresentationSnapshotBuilder.PrimarySurfaceId, StringComparison.OrdinalIgnoreCase))
            {
                return _viewState.OverlayPrimaryWindow;
            }

            if (string.Equals(surfaceId, OverlayPresentationSnapshotBuilder.DocumentSurfaceId, StringComparison.OrdinalIgnoreCase))
            {
                return _viewState.OverlayDocumentWindow;
            }

            if (string.Equals(surfaceId, OverlayPresentationSnapshotBuilder.SecondarySurfaceId, StringComparison.OrdinalIgnoreCase))
            {
                return _viewState.OverlaySecondaryWindow;
            }

            if (string.Equals(surfaceId, OverlayPresentationSnapshotBuilder.PanelSurfaceId, StringComparison.OrdinalIgnoreCase))
            {
                return _viewState.OverlayPanelWindow;
            }

            return null;
        }

        private static string BuildFingerprint(OverlayPresentationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Surfaces == null)
            {
                return string.Empty;
            }

            var parts = new System.Collections.Generic.List<string>();
            parts.Add(snapshot.PresentationModeId ?? string.Empty);
            parts.Add(snapshot.ActiveSurfaceId ?? string.Empty);
            parts.Add(snapshot.FocusedRegionId ?? string.Empty);
            for (var i = 0; i < snapshot.Surfaces.Count; i++)
            {
                var surface = snapshot.Surfaces[i];
                if (surface == null)
                {
                    continue;
                }

                parts.Add(
                    (surface.SurfaceId ?? string.Empty) + ":" +
                    surface.Visible + ":" +
                    surface.IsCollapsed + ":" +
                    surface.Bounds.X + ":" +
                    surface.Bounds.Y + ":" +
                    surface.Bounds.Width + ":" +
                    surface.Bounds.Height + ":" +
                    (surface.ContentViewId ?? string.Empty) + ":" +
                    (surface.ActiveContainerId ?? string.Empty));
            }

            return string.Join("|", parts.ToArray());
        }
    }
}
