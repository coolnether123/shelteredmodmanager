using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.UiKit.Animation;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private readonly List<ScenarioAuthoringVisualSurface> _visualSurfaces =
            new List<ScenarioAuthoringVisualSurface>();
        private readonly List<string> _visualSurfaceStack = new List<string>();
        private readonly List<bool> _visualSurfaceEnabledStack = new List<bool>();
        private int _visualSurfaceOrder;

        private void BeginVisualSurfaceFrame()
        {
            _visualSurfaces.Clear();
            _visualSurfaceStack.Clear();
            _visualSurfaceEnabledStack.Clear();
            _visualSurfaceOrder = 0;
        }

        private void RegisterVisualSurface(string id, Rect rect)
        {
            if (string.IsNullOrEmpty(id) || rect.width <= 0f || rect.height <= 0f)
                return;

            _visualSurfaces.Add(new ScenarioAuthoringVisualSurface
            {
                Id = id,
                Rect = rect,
                Order = _visualSurfaceOrder++
            });
        }

        private void RegisterWindowVisualSurfaces(
            ScenarioAuthoringShellWindowViewModel[] windows,
            Dictionary<string, Rect> windowRects,
            bool floating)
        {
            ScenarioAuthoringShellWindowViewModel[] drawList = BuildWindowDrawList(windows, floating);
            for (int i = 0; i < drawList.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = drawList[i];
                Rect rect;
                if (window == null
                    || !window.Visible
                    || window.Collapsed
                    || windowRects == null
                    || !windowRects.TryGetValue(window.Id, out rect))
                {
                    continue;
                }

                ScenarioAuthoringShellAnimationService.WindowVisualState visual = _animations.GetWindowVisual(window.Id);
                if (visual != null && visual.Alpha <= WindowInteractionAlphaThreshold)
                    continue;

                float slideProgress = visual != null ? (1f - visual.Slide) : 1f;
                RegisterVisualSurface(VisualSurfaceIdForWindow(window.Id), ResolveWindowSlidingRect(rect, slideProgress));
            }
        }

        private static string VisualSurfaceIdForWindow(string windowId)
        {
            return "window:" + (windowId ?? string.Empty);
        }

        private IDisposable EnterVisualSurface(string id)
        {
            return EnterVisualSurface(id, true);
        }

        private IDisposable EnterVisualSurface(string id, bool enabled)
        {
            _visualSurfaceStack.Add(id);
            _visualSurfaceEnabledStack.Add(enabled);
            return new ScenarioAuthoringVisualSurfaceScope(this);
        }

        private string CurrentVisualSurfaceId
        {
            get
            {
                return _visualSurfaceStack.Count == 0
                    ? null
                    : _visualSurfaceStack[_visualSurfaceStack.Count - 1];
            }
        }

        private bool IsInteractiveVisualTopmost(Rect rect)
        {
            if (_visualSurfaceEnabledStack.Count > 0 && !_visualSurfaceEnabledStack[_visualSurfaceEnabledStack.Count - 1])
                return false;

            Vector2 pointer = GetCurrentEventPointer();
            if (!rect.Contains(pointer))
                return true;

            ScenarioAuthoringVisualSurface topmost = ResolveTopmostVisualSurface(pointer);
            if (topmost == null)
                return true;

            string current = CurrentVisualSurfaceId;
            return !string.IsNullOrEmpty(current)
                && string.Equals(topmost.Id, current, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsInteractiveHoverAllowed(Rect rect)
        {
            return _scaledWindowDrawDepth == 0
                && Event.current != null
                && rect.Contains(Event.current.mousePosition)
                && IsInteractiveVisualTopmost(rect);
        }

        private bool IsInteractiveMouseDownAllowed(Rect rect)
        {
            Event evt = Event.current;
            return evt != null
                && evt.type == EventType.MouseDown
                && evt.button == 0
                && rect.Contains(evt.mousePosition)
                && IsInteractiveVisualTopmost(rect);
        }

        private bool DrawPlainButton(Rect rect, GUIContent content, GUIStyle style, bool enabled)
        {
            RegisterInteractiveRegion(rect);
            bool canUseNativeControl = enabled && IsInteractiveVisualTopmost(rect);
            if (canUseNativeControl)
                return GUI.Button(rect, content, style);

            GUI.Box(rect, content, style);
            return false;
        }

        private Vector2 GetCurrentEventPointer()
        {
            Event evt = Event.current;
            if (evt != null)
            {
                if (UnityEngine.Input.GetMouseButton(0)
                    || UnityEngine.Input.GetMouseButtonDown(0)
                    || UnityEngine.Input.GetMouseButtonUp(0))
                {
                    return new Vector2(UnityEngine.Input.mousePosition.x / _activeUiScale, (Screen.height - UnityEngine.Input.mousePosition.y) / _activeUiScale);
                }

                return evt.mousePosition;
            }

            return new Vector2(UnityEngine.Input.mousePosition.x / _activeUiScale, (Screen.height - UnityEngine.Input.mousePosition.y) / _activeUiScale);
        }

        private ScenarioAuthoringVisualSurface ResolveTopmostVisualSurface(Vector2 pointer)
        {
            ScenarioAuthoringVisualSurface topmost = null;
            for (int i = 0; i < _visualSurfaces.Count; i++)
            {
                ScenarioAuthoringVisualSurface surface = _visualSurfaces[i];
                if (surface != null
                    && surface.Rect.Contains(pointer)
                    && (topmost == null || surface.Order >= topmost.Order))
                {
                    topmost = surface;
                }
            }

            return topmost;
        }

        private sealed class ScenarioAuthoringVisualSurface
        {
            public string Id;
            public Rect Rect;
            public int Order;
        }

        private sealed class ScenarioAuthoringVisualSurfaceScope : IDisposable
        {
            private readonly ScenarioAuthoringShellImguiRenderModule _owner;

            public ScenarioAuthoringVisualSurfaceScope(ScenarioAuthoringShellImguiRenderModule owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner != null && _owner._visualSurfaceStack.Count > 0)
                {
                    _owner._visualSurfaceStack.RemoveAt(_owner._visualSurfaceStack.Count - 1);
                    if (_owner._visualSurfaceEnabledStack.Count > 0)
                        _owner._visualSurfaceEnabledStack.RemoveAt(_owner._visualSurfaceEnabledStack.Count - 1);
                }
            }
        }
    }
}
