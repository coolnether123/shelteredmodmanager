using System;
using Cortex.Core.Diagnostics;
using UnityEngine;

namespace Cortex.Renderers.DearImgui
{
    public sealed class DearImguiPresentationBehaviour : MonoBehaviour
    {
        private const float RenderWatchdogSeconds = 1.25f;
        private static readonly CortexLogger Log = CortexLog.ForSource("Cortex.DearImgui");
        private DearImguiUnityBackend _backend;
        private CortexShellController _controller;
        private float _rendererActivatedAt = -1f;
        private float _lastSuccessfulRenderAt = -1f;
        private float _visibleActivatedAt = -1f;
        private float _lastSuccessfulVisibleRenderAt = -1f;
        private bool _watchdogFallbackIssued;
        private bool _loggedFirstSuccessfulRender;
        private bool _loggedFirstSuccessfulVisibleRenderForCurrentOpen;
        private bool _lastObservedShellVisible;

        public void Configure(CortexShellController controller)
        {
            _controller = controller;
            if (_backend == null)
            {
                _backend = new DearImguiUnityBackend();
            }
        }

        public void RenderOnGui()
        {
            if (_controller == null || _backend == null)
            {
                return;
            }

            if (!string.Equals(_controller.CurrentRendererId, DearImguiWorkbenchRenderer.RendererIdValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type != EventType.Repaint)
            {
                return;
            }

            var shellVisible = _controller.IsShellVisibleForRenderer;
            if (_backend.Render(_controller))
            {
                _lastSuccessfulRenderAt = Time.realtimeSinceStartup;
                if (shellVisible)
                {
                    _lastSuccessfulVisibleRenderAt = _lastSuccessfulRenderAt;
                }

                if (!_loggedFirstSuccessfulRender)
                {
                    _loggedFirstSuccessfulRender = true;
                    Log.WriteInfo(
                        "Dear ImGui rendered successfully for the first time. Frame=" + Time.frameCount +
                        ", ShellVisible=" + shellVisible +
                        ", Host=OnGUI" +
                        ", Backend=" + _backend.DescribeLastRenderStats());
                }

                if (shellVisible && !_loggedFirstSuccessfulVisibleRenderForCurrentOpen)
                {
                    _loggedFirstSuccessfulVisibleRenderForCurrentOpen = true;
                    Log.WriteInfo(
                        "Dear ImGui rendered first visible shell frame for current open. Frame=" + Time.frameCount +
                        ", Host=OnGUI" +
                        ", Backend=" + _backend.DescribeLastRenderStats());
                }
            }
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            var isDearImguiActive = string.Equals(_controller.CurrentRendererId, DearImguiWorkbenchRenderer.RendererIdValue, StringComparison.OrdinalIgnoreCase);
            if (!isDearImguiActive)
            {
                ResetWatchdogState();
                return;
            }

            if (_rendererActivatedAt < 0f)
            {
                _rendererActivatedAt = Time.realtimeSinceStartup;
                _lastSuccessfulRenderAt = -1f;
                _visibleActivatedAt = -1f;
                _lastSuccessfulVisibleRenderAt = -1f;
                _watchdogFallbackIssued = false;
                _loggedFirstSuccessfulRender = false;
                _loggedFirstSuccessfulVisibleRenderForCurrentOpen = false;
                _lastObservedShellVisible = _controller.IsShellVisibleForRenderer;
                Log.WriteInfo("Dear ImGui presentation activated. ShellVisible=" + _controller.IsShellVisibleForRenderer + ", Host=OnGUI, Frame=" + Time.frameCount + ".");
            }

            var shellVisible = _controller.IsShellVisibleForRenderer;
            if (shellVisible != _lastObservedShellVisible)
            {
                _lastObservedShellVisible = shellVisible;
                var frame = _controller.CreateShellFrameForRenderer();
                var window = frame != null ? frame.MainWindow : null;
                Log.WriteInfo(
                    "Dear ImGui shell visibility observed. Visible=" + shellVisible +
                    ", MainWindow=(" +
                    (window != null ? window.X.ToString("F0") : "0") + "," +
                    (window != null ? window.Y.ToString("F0") : "0") + "," +
                    (window != null ? window.Width.ToString("F0") : "0") + "," +
                    (window != null ? window.Height.ToString("F0") : "0") + ")" +
                    ", Collapsed=" + (window != null && window.IsCollapsed) +
                    ", Frame=" + Time.frameCount + ".");
                if (shellVisible)
                {
                    _visibleActivatedAt = Time.realtimeSinceStartup;
                    _lastSuccessfulVisibleRenderAt = -1f;
                    _loggedFirstSuccessfulVisibleRenderForCurrentOpen = false;
                }
                else
                {
                    _visibleActivatedAt = -1f;
                    _lastSuccessfulVisibleRenderAt = -1f;
                    _loggedFirstSuccessfulVisibleRenderForCurrentOpen = false;
                }
            }

            if (_watchdogFallbackIssued || !shellVisible)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            var secondsWithoutSuccessfulVisibleRender = _lastSuccessfulVisibleRenderAt >= 0f
                ? now - _lastSuccessfulVisibleRenderAt
                : _visibleActivatedAt >= 0f
                    ? now - _visibleActivatedAt
                    : now - _rendererActivatedAt;
            if (secondsWithoutSuccessfulVisibleRender < RenderWatchdogSeconds)
            {
                return;
            }

            _watchdogFallbackIssued = true;
            var watchdogFrame = _controller.CreateShellFrameForRenderer();
            var watchdogWindow = watchdogFrame != null ? watchdogFrame.MainWindow : null;
            var detail =
                "SecondsWithoutSuccessfulVisibleRender=" + secondsWithoutSuccessfulVisibleRender.ToString("F2") +
                ", LastSuccessfulRenderAt=" + _lastSuccessfulRenderAt.ToString("F2") +
                ", LastSuccessfulVisibleRenderAt=" + _lastSuccessfulVisibleRenderAt.ToString("F2") +
                ", ActivatedAt=" + _rendererActivatedAt.ToString("F2") +
                ", VisibleActivatedAt=" + _visibleActivatedAt.ToString("F2") +
                ", Frame=" + Time.frameCount +
                ", ShellVisible=" + shellVisible +
                ", MainWindow=(" +
                (watchdogWindow != null ? watchdogWindow.X.ToString("F0") : "0") + "," +
                (watchdogWindow != null ? watchdogWindow.Y.ToString("F0") : "0") + "," +
                (watchdogWindow != null ? watchdogWindow.Width.ToString("F0") : "0") + "," +
                (watchdogWindow != null ? watchdogWindow.Height.ToString("F0") : "0") + ")" +
                ", Collapsed=" + (watchdogWindow != null && watchdogWindow.IsCollapsed) + ".";
            Log.WriteWarning("Dear ImGui watchdog triggered. " + detail);
            _controller.FallbackDearImguiToImguiFromRenderer(
                _lastSuccessfulVisibleRenderAt >= 0f ? "dearimgui-visible-render-stalled" : "dearimgui-visible-render-never-started",
                detail);
        }

        private void OnRenderObject()
        {
            // Dear ImGui is hosted from OnGUI so it shares the same presentation path as IMGUI.
        }

        private void OnDestroy()
        {
            if (_backend != null)
            {
                _backend.Dispose();
                _backend = null;
            }

            ResetWatchdogState();
        }

        private void ResetWatchdogState()
        {
            _rendererActivatedAt = -1f;
            _lastSuccessfulRenderAt = -1f;
            _visibleActivatedAt = -1f;
            _lastSuccessfulVisibleRenderAt = -1f;
            _watchdogFallbackIssued = false;
            _loggedFirstSuccessfulRender = false;
            _loggedFirstSuccessfulVisibleRenderForCurrentOpen = false;
            _lastObservedShellVisible = false;
        }
    }
}
