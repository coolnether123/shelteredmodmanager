using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cortex.Bridge;
using Cortex.Host.Avalonia.Services;
using Serilog;

namespace Cortex.Host.Avalonia.Views
{
    internal sealed class OverlaySurfaceWindow : Window
    {
        private readonly TextBlock _titleBlock;
        private readonly TextBlock _subtitleBlock;
        private readonly ContentControl _contentHost;
        private string _contentViewId = string.Empty;
        private string _surfaceId = string.Empty;
        private bool _lastVisible;
        private string _lastBoundsKey = string.Empty;
        private bool _lastInteractive;

        public OverlaySurfaceWindow()
        {
            SystemDecorations = SystemDecorations.None;
            ShowInTaskbar = false;
            CanResize = false;
            Topmost = true;
            Background = Brushes.Transparent;
            Opacity = 0d;
            Width = 1d;
            Height = 1d;

            _titleBlock = new TextBlock
            {
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White
            };
            _subtitleBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#9CA8BC")),
                FontSize = 12
            };
            _contentHost = new ContentControl();
            var contentBorder = new Border
            {
                Child = _contentHost
            };
            Grid.SetRow(contentBorder, 1);

            Content = new Border
            {
                Margin = new Thickness(0),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.Parse("#DD151924")),
                BorderBrush = new SolidColorBrush(Color.Parse("#314056")),
                BorderThickness = new Thickness(1),
                Child = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*"),
                    RowSpacing = 10,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 2,
                            Children =
                            {
                                _titleBlock,
                                _subtitleBlock
                            }
                        },
                        contentBorder
                    }
                }
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            DesktopOverlayWindowInterop.Detach(this);
            base.OnClosed(e);
        }

        public void ApplySurface(OverlaySurfaceSnapshot surface, DesktopOverlaySurfaceViewFactory viewFactory)
        {
            var resolvedSurface = surface ?? new OverlaySurfaceSnapshot();
            _surfaceId = resolvedSurface.SurfaceId ?? _surfaceId ?? string.Empty;
            _titleBlock.Text = resolvedSurface.Chrome != null ? resolvedSurface.Chrome.Title ?? string.Empty : string.Empty;
            _subtitleBlock.Text = resolvedSurface.Chrome != null ? resolvedSurface.Chrome.Subtitle ?? string.Empty : string.Empty;

            var nextContentViewId = resolvedSurface.ContentViewId ?? string.Empty;
            if (!string.Equals(_contentViewId, nextContentViewId, StringComparison.Ordinal))
            {
                _contentViewId = nextContentViewId;
                Log.Information("Overlay surface content changed. SurfaceId={SurfaceId}, ContentViewId={ContentViewId}", _surfaceId, _contentViewId);
                _contentHost.Content = viewFactory != null ? viewFactory.Create(nextContentViewId) : null;
            }

            Width = Math.Max(1d, resolvedSurface.Bounds != null ? resolvedSurface.Bounds.Width : 1d);
            Height = Math.Max(1d, resolvedSurface.Bounds != null ? resolvedSurface.Bounds.Height : 1d);
            Opacity = resolvedSurface.Visible ? 1d : 0d;
            if (_lastVisible != resolvedSurface.Visible)
            {
                _lastVisible = resolvedSurface.Visible;
                Log.Information("Overlay surface visibility changed. SurfaceId={SurfaceId}, Visible={Visible}", _surfaceId, _lastVisible);
            }
        }

        public void ApplyScreenBounds(int x, int y, bool interactive, IList<OverlayHitRegion> hitRegions)
        {
            Position = new PixelPoint(x, y);
            var boundsKey = x + "," + y + "," + (int)Math.Round(Width) + "," + (int)Math.Round(Height);
            if (!string.Equals(_lastBoundsKey, boundsKey, StringComparison.Ordinal))
            {
                _lastBoundsKey = boundsKey;
                Log.Information("Overlay surface bounds changed. SurfaceId={SurfaceId}, Bounds={Bounds}", _surfaceId, boundsKey);
            }

            if (_lastInteractive != interactive)
            {
                _lastInteractive = interactive;
                Log.Information(
                    "Overlay surface interactivity changed. SurfaceId={SurfaceId}, Interactive={Interactive}, HitRegionCount={HitRegionCount}",
                    _surfaceId,
                    interactive,
                    hitRegions != null ? hitRegions.Count : 0);
            }

            DesktopOverlayWindowInterop.ApplyOverlayStyle(this, interactive, hitRegions);
        }
    }
}
