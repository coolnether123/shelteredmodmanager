using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Cortex.Bridge;
using Cortex.Host.Avalonia.Bridge;
using Cortex.Host.Avalonia.ViewModels;
using Cortex.Host.Avalonia.Views;
using Serilog;

namespace Cortex.Host.Avalonia.Services
{
    internal sealed class DesktopOverlayWindowManager : IDisposable
    {
        private const string ControlSurfaceId = "overlay.control";
        private readonly NamedPipeDesktopBridgeClient _bridgeClient;
        private readonly DesktopOverlaySurfaceViewFactory _viewFactory;
        private readonly DesktopGameWindowTracker _gameWindowTracker = new DesktopGameWindowTracker();
        private readonly Dictionary<string, OverlaySurfaceWindow> _windows = new Dictionary<string, OverlaySurfaceWindow>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _lastSurfaceSummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _positionRefreshTimer;
        private OverlayPresentationSnapshot _lastSnapshot = new OverlayPresentationSnapshot();
        private string _lastGameWindowState = string.Empty;
        private bool _loggedFirstSnapshot;

        public DesktopOverlayWindowManager(
            NamedPipeDesktopBridgeClient bridgeClient,
            MainWindowViewModel workbenchViewModel,
            DesktopShellViewModel shellViewModel)
        {
            _bridgeClient = bridgeClient;
            _viewFactory = new DesktopOverlaySurfaceViewFactory(workbenchViewModel, shellViewModel);
            _bridgeClient.OverlaySnapshotReceived += snapshot => Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));
            _bridgeClient.OverlayLifecycleReceived += lifecycle => Dispatcher.UIThread.Post(() => HandleLifecycle(lifecycle));
            _positionRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _positionRefreshTimer.Tick += delegate { RefreshWindowPositions(); };
            _positionRefreshTimer.Start();
        }

        public Window GetOrCreatePrimaryWindow()
        {
            return GetOrCreateWindow(ControlSurfaceId);
        }

        public void Dispose()
        {
            _positionRefreshTimer.Stop();
            foreach (var pair in _windows)
            {
                pair.Value.Close();
            }

            _windows.Clear();
        }

        private void ApplySnapshot(OverlayPresentationSnapshot snapshot)
        {
            _lastSnapshot = snapshot ?? new OverlayPresentationSnapshot();
            var visibleSurfaceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bounds = ResolveGameWindowBounds(_lastSnapshot);
            ReportGameWindowState(bounds);
            if (!bounds.IsVisible || bounds.IsMinimized || bounds.Width <= 0 || bounds.Height <= 0)
            {
                HideAllWindows();
                return;
            }

            if (!_loggedFirstSnapshot)
            {
                _loggedFirstSnapshot = true;
                Log.Information(
                    "Applying first overlay snapshot. PresentationMode={PresentationMode}, SurfaceCount={SurfaceCount}, GameWindowBounds={X},{Y},{Width},{Height}",
                    _lastSnapshot.PresentationModeId ?? string.Empty,
                    _lastSnapshot.Surfaces != null ? _lastSnapshot.Surfaces.Count : 0,
                    bounds.X,
                    bounds.Y,
                    bounds.Width,
                    bounds.Height);
            }

            foreach (var surface in _lastSnapshot.Surfaces ?? new List<OverlaySurfaceSnapshot>())
            {
                if (surface == null)
                {
                    continue;
                }

                visibleSurfaceIds.Add(surface.SurfaceId ?? string.Empty);
                LogSurfaceSnapshot(surface);
                var window = GetOrCreateWindow(surface.SurfaceId);
                window.ApplySurface(surface, _viewFactory);
                window.ApplyScreenBounds(
                    bounds.X + (int)Math.Round(surface.Bounds != null ? surface.Bounds.X : 0d),
                    bounds.Y + (int)Math.Round(surface.Bounds != null ? surface.Bounds.Y : 0d),
                    HasInteractiveRegions(surface),
                    surface.HitRegions);

                if (surface.Visible && !window.IsVisible)
                {
                    Log.Information("Showing overlay surface window. SurfaceId={SurfaceId}", surface.SurfaceId ?? string.Empty);
                    window.Show();
                }
                else if (!surface.Visible && window.IsVisible)
                {
                    Log.Information("Hiding overlay surface window because the surface is not visible. SurfaceId={SurfaceId}", surface.SurfaceId ?? string.Empty);
                    window.Hide();
                }
            }

            foreach (var pair in _windows)
            {
                if (!visibleSurfaceIds.Contains(pair.Key) && pair.Value.IsVisible)
                {
                    Log.Information("Hiding overlay surface window because it is absent from the current snapshot. SurfaceId={SurfaceId}", pair.Key);
                    pair.Value.Hide();
                }
            }
        }

        private void RefreshWindowPositions()
        {
            if (_lastSnapshot == null || _lastSnapshot.Surfaces == null || _lastSnapshot.Surfaces.Count == 0)
            {
                return;
            }

            var bounds = ResolveGameWindowBounds(_lastSnapshot);
            if (!bounds.IsVisible || bounds.IsMinimized || bounds.Width <= 0 || bounds.Height <= 0)
            {
                HideAllWindows();
                return;
            }

            foreach (var surface in _lastSnapshot.Surfaces)
            {
                if (surface == null || string.IsNullOrEmpty(surface.SurfaceId))
                {
                    continue;
                }

                OverlaySurfaceWindow window;
                if (!_windows.TryGetValue(surface.SurfaceId, out window) || window == null)
                {
                    continue;
                }

                window.ApplyScreenBounds(
                    bounds.X + (int)Math.Round(surface.Bounds != null ? surface.Bounds.X : 0d),
                    bounds.Y + (int)Math.Round(surface.Bounds != null ? surface.Bounds.Y : 0d),
                    HasInteractiveRegions(surface),
                    surface.HitRegions);
            }
        }

        private void HandleLifecycle(OverlayHostLifecycleMessage lifecycle)
        {
            if (lifecycle == null)
            {
                return;
            }

            if (lifecycle.Kind == OverlayHostLifecycleKind.ShutdownRequested)
            {
                Log.Information("Received shutdown request from runtime. LaunchToken={LaunchToken}", lifecycle.LaunchToken ?? string.Empty);
                _bridgeClient.TrySendOverlayLifecycle(new OverlayHostLifecycleMessage
                {
                    Revision = lifecycle.Revision,
                    Kind = OverlayHostLifecycleKind.ShutdownAcknowledged,
                    LaunchToken = lifecycle.LaunchToken ?? string.Empty,
                    StatusMessage = "External overlay host acknowledged shutdown.",
                    UtcTimestamp = DateTime.UtcNow.ToString("o")
                });

                var desktop = Application.Current != null
                    ? Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime
                    : null;
                if (desktop != null)
                {
                    desktop.Shutdown();
                }
            }
        }

        private OverlaySurfaceWindow GetOrCreateWindow(string surfaceId)
        {
            var resolvedSurfaceId = string.IsNullOrEmpty(surfaceId)
                ? ControlSurfaceId
                : surfaceId;
            OverlaySurfaceWindow window;
            if (_windows.TryGetValue(resolvedSurfaceId, out window))
            {
                return window;
            }

            window = new OverlaySurfaceWindow();
            Log.Information("Created overlay surface window. SurfaceId={SurfaceId}", resolvedSurfaceId);
            window.Activated += delegate
            {
                _bridgeClient.TrySendOverlayInputIntent(new OverlayInputIntentMessage
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    SurfaceId = resolvedSurfaceId,
                    IntentType = OverlayInputIntentType.FocusSurface
                });
            };
            window.PointerEntered += delegate(object sender, PointerEventArgs args)
            {
                _bridgeClient.TrySendOverlayInputIntent(new OverlayInputIntentMessage
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    SurfaceId = resolvedSurfaceId,
                    IntentType = OverlayInputIntentType.PointerEnter
                });
            };
            window.PointerExited += delegate(object sender, PointerEventArgs args)
            {
                _bridgeClient.TrySendOverlayInputIntent(new OverlayInputIntentMessage
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    SurfaceId = resolvedSurfaceId,
                    IntentType = OverlayInputIntentType.PointerLeave
                });
            };
            window.Deactivated += delegate
            {
                _bridgeClient.TrySendOverlayInputIntent(new OverlayInputIntentMessage
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    SurfaceId = resolvedSurfaceId,
                    IntentType = OverlayInputIntentType.BlurSurface
                });
            };

            _windows[resolvedSurfaceId] = window;
            return window;
        }

        private void HideAllWindows()
        {
            foreach (var pair in _windows)
            {
                if (pair.Value.IsVisible)
                {
                    Log.Information("Hiding overlay surface window because the game window is unavailable. SurfaceId={SurfaceId}", pair.Key);
                    pair.Value.Hide();
                }
            }
        }

        private DesktopGameWindowTracker.PixelBounds ResolveGameWindowBounds(OverlayPresentationSnapshot snapshot)
        {
            DesktopGameWindowTracker.PixelBounds bounds;
            if (snapshot != null &&
                snapshot.GameWindow != null &&
                snapshot.GameWindow.ProcessId > 0 &&
                _gameWindowTracker.TryGetWindowBounds(snapshot.GameWindow.ProcessId, out bounds))
            {
                return bounds;
            }

            return new DesktopGameWindowTracker.PixelBounds(0, 0, 0, 0, false, false);
        }

        private void ReportGameWindowState(DesktopGameWindowTracker.PixelBounds bounds)
        {
            var state =
                !bounds.IsVisible ? "unresolved" :
                bounds.IsMinimized ? "minimized" :
                bounds.Width <= 0 || bounds.Height <= 0 ? "empty" :
                "attached:" + bounds.X + "," + bounds.Y + "," + bounds.Width + "," + bounds.Height;
            if (string.Equals(_lastGameWindowState, state, StringComparison.Ordinal))
            {
                return;
            }

            _lastGameWindowState = state;
            Log.Information("Overlay game window state changed. State={State}", state);
        }

        private void LogSurfaceSnapshot(OverlaySurfaceSnapshot surface)
        {
            var surfaceId = surface != null ? surface.SurfaceId ?? string.Empty : string.Empty;
            var summary =
                (surface != null ? surface.Visible.ToString() : string.Empty) + "|" +
                (surface != null ? surface.IsCollapsed.ToString() : string.Empty) + "|" +
                (surface != null && surface.Bounds != null ? surface.Bounds.X.ToString() : string.Empty) + "|" +
                (surface != null && surface.Bounds != null ? surface.Bounds.Y.ToString() : string.Empty) + "|" +
                (surface != null && surface.Bounds != null ? surface.Bounds.Width.ToString() : string.Empty) + "|" +
                (surface != null && surface.Bounds != null ? surface.Bounds.Height.ToString() : string.Empty) + "|" +
                (surface != null ? surface.ContentViewId ?? string.Empty : string.Empty);
            string previousSummary;
            if (_lastSurfaceSummaries.TryGetValue(surfaceId, out previousSummary) &&
                string.Equals(previousSummary, summary, StringComparison.Ordinal))
            {
                return;
            }

            _lastSurfaceSummaries[surfaceId] = summary;
            Log.Information(
                "Overlay surface snapshot changed. SurfaceId={SurfaceId}, Visible={Visible}, Collapsed={Collapsed}, Bounds={X},{Y},{Width},{Height}, ContentViewId={ContentViewId}",
                surfaceId,
                surface != null && surface.Visible,
                surface != null && surface.IsCollapsed,
                surface != null && surface.Bounds != null ? surface.Bounds.X : 0d,
                surface != null && surface.Bounds != null ? surface.Bounds.Y : 0d,
                surface != null && surface.Bounds != null ? surface.Bounds.Width : 0d,
                surface != null && surface.Bounds != null ? surface.Bounds.Height : 0d,
                surface != null ? surface.ContentViewId ?? string.Empty : string.Empty);
        }

        private static bool HasInteractiveRegions(OverlaySurfaceSnapshot surface)
        {
            if (surface == null || surface.HitRegions == null)
            {
                return false;
            }

            for (var i = 0; i < surface.HitRegions.Count; i++)
            {
                if (surface.HitRegions[i] != null && surface.HitRegions[i].Interactive)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
