using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Infrastructure.Unity;

namespace ShelteredAPI.Scenarios.Presentation.UiKit.Animation
{
    internal sealed class ScenarioAuthoringShellAnimationService
    {
        private const float WindowOpenDuration = 0.18f;
        private const float WindowCloseDuration = 0.12f;
        private const float ButtonHoverDuration = 0.16f;
        private const float ButtonPressDuration = 0.08f;
        private const float ButtonRecoverDuration = 0.12f;
        private const float PopupDuration = 0.15f;
        private const float TooltipDuration = 0.12f;
        private const float TooltipShowDelaySeconds = 0.40f;
        private const float ModalDimDuration = 0.15f;
        private const float ModalPanelDuration = 0.18f;
        private const float ToastInDuration = 0.20f;
        private const float ToastOutDuration = 0.15f;
        private const float ToastHoldSeconds = 2.75f;

        private const string WindowMenuKey = "popup.window_menu";
        private const string ContextMenuKey = "popup.context_menu";
        private const string TooltipKey = "tooltip.current";
        private const string ModalDimKey = "modal.sprite_picker.dim";
        private const string ModalPanelKey = "modal.sprite_picker.panel";
        private const string HelpModalDimKey = "modal.help.dim";
        private const string HelpModalPanelKey = "modal.help.panel";
        private const string TutorialOverlayKey = "modal.tutorial.overlay";
        private const string ToastKey = "toast.status";

        private readonly ScenarioUiTweenSet _tweens = new ScenarioUiTweenSet();
        private readonly Dictionary<string, WindowVisualState> _windows = new Dictionary<string, WindowVisualState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _buttonPressKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pulseSignatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _windowRemovalBuffer = new List<string>();
        private bool _enabled = true;
        private int _lastFrame = -1;
        private string _lastTooltip;
        private string _lastStatus;
        private float _statusChangedAt;
        private bool _modalVisibleLastFrame;
        private float _tooltipChangedAt;
        private int _tooltipLastSeenFrame = -1000;

        public bool Enabled
        {
            get { return _enabled; }
        }

        public bool TransitionActive { get; private set; }

        public void BeginFrame(ScenarioAuthoringSettingsSnapshot settings)
        {
            _enabled = ShouldAnimate(settings);
            int frame = Time.frameCount;
            if (_lastFrame == frame)
                return;

            _lastFrame = frame;
            float deltaTime = _enabled ? Time.unscaledDeltaTime : 1f;
            _tweens.Advance(deltaTime);
            TransitionActive = false;
        }

        public void RegisterWindow(ScenarioAuthoringShellWindowViewModel window, Rect rect)
        {
            if (window == null || string.IsNullOrEmpty(window.Id))
                return;

            WindowVisualState state = GetWindowState(window.Id);
            state.Window = window;
            state.LastRect = rect;
            state.Floating = window.Dock == ScenarioAuthoringShellDock.Floating;
            state.VisibleThisFrame = window.Visible && !window.Collapsed;
            state.RegisteredThisFrame = true;

            if (state.VisibleThisFrame && (!state.WasVisible || state.Closing))
            {
                PlayWindowOpen(state);
            }
            else if (state.WasVisible && !state.VisibleThisFrame)
            {
                PlayWindowClose(state);
            }
            else if (!_enabled && state.VisibleThisFrame)
            {
                state.Alpha = 1f;
                state.Scale = 1f;
                state.Closing = false;
            }

            if (state.VisibleThisFrame)
            {
                state.Alpha = _tweens.GetValue(state.AlphaKey, 1f);
                state.Scale = _tweens.GetValue(state.ScaleKey, 1f);
                if (_tweens.IsRunning(state.AlphaKey) || _tweens.IsRunning(state.ScaleKey))
                    TransitionActive = true;
            }

            state.WasVisible = state.VisibleThisFrame;
        }

        public void CompleteWindowRegistration()
        {
            _windowRemovalBuffer.Clear();
            foreach (KeyValuePair<string, WindowVisualState> pair in _windows)
            {
                WindowVisualState state = pair.Value;
                if (state == null)
                    continue;

                if (!state.RegisteredThisFrame && state.WasVisible)
                    PlayWindowClose(state);

                if (state.Closing)
                {
                    state.Alpha = _tweens.GetValue(state.AlphaKey, 0f);
                    state.Scale = _tweens.GetValue(state.ScaleKey, 0.985f);
                    if (_tweens.IsRunning(state.AlphaKey) || _tweens.IsRunning(state.ScaleKey))
                        TransitionActive = true;
                    else if (state.Alpha <= 0.001f)
                    {
                        state.Closing = false;
                        _windowRemovalBuffer.Add(pair.Key);
                    }
                }

                state.WasVisible = state.RegisteredThisFrame && state.VisibleThisFrame;
                state.VisibleThisFrame = false;
                state.RegisteredThisFrame = false;
            }

            for (int i = 0; i < _windowRemovalBuffer.Count; i++)
            {
                WindowVisualState removed;
                if (_windows.TryGetValue(_windowRemovalBuffer[i], out removed))
                {
                    _tweens.Remove(removed.AlphaKey);
                    _tweens.Remove(removed.ScaleKey);
                    _windows.Remove(_windowRemovalBuffer[i]);
                }
            }
        }

        public WindowVisualState GetWindowVisual(string windowId)
        {
            WindowVisualState state;
            return !string.IsNullOrEmpty(windowId) && _windows.TryGetValue(windowId, out state) ? state : null;
        }

        public void UpdateWindowRect(string windowId, Rect rect)
        {
            WindowVisualState state = GetWindowVisual(windowId);
            if (state != null)
                state.LastRect = rect;
        }

        public void CollectClosingWindows(bool floating, List<WindowVisualState> results)
        {
            if (results == null)
                return;

            results.Clear();
            foreach (KeyValuePair<string, WindowVisualState> pair in _windows)
            {
                WindowVisualState state = pair.Value;
                if (state != null && state.Closing && state.Floating == floating && state.Window != null)
                    results.Add(state);
            }
        }

        public float GetButtonHover(string actionId, bool hovered)
        {
            if (string.IsNullOrEmpty(actionId) || !_enabled)
                return hovered ? 1f : 0f;

            _tweens.PlayFromCurrent(actionId, hovered ? 1f : 0f, ButtonHoverDuration, ScenarioUiEasing.EaseInOut, hovered ? 1f : 0f);
            return _tweens.GetValue(actionId, hovered ? 1f : 0f);
        }

        public float GetButtonPress(string key, bool pressed)
        {
            if (string.IsNullOrEmpty(key) || !_enabled)
                return pressed ? 1f : 0f;

            _tweens.PlayFromCurrent(key, pressed ? 1f : 0f, pressed ? ButtonPressDuration : ButtonRecoverDuration, ScenarioUiEasing.EaseInOut, 0f);
            return _tweens.GetValue(key, pressed ? 1f : 0f);
        }

        public float GetButtonPressForAction(string actionId, bool pressed)
        {
            if (string.IsNullOrEmpty(actionId))
                return pressed ? 1f : 0f;

            string key;
            if (!_buttonPressKeys.TryGetValue(actionId, out key))
            {
                key = string.Concat("button:", actionId, ":press");
                _buttonPressKeys.Add(actionId, key);
            }

            return GetButtonPress(key, pressed);
        }

        public float GetPopupProgress(bool visible, bool windowMenu)
        {
            return GetBinaryProgress(windowMenu ? WindowMenuKey : ContextMenuKey, visible, PopupDuration, ScenarioUiEasing.PopupOut, false);
        }

        public float GetTooltipAlpha(string tooltip)
        {
            bool visible = !string.IsNullOrEmpty(tooltip);
            if (!string.Equals(_lastTooltip, tooltip, StringComparison.Ordinal))
            {
                _lastTooltip = tooltip;
                _tooltipChangedAt = Time.realtimeSinceStartup;
                _tweens.Set(TooltipKey, 0f);
            }

            if (visible && Time.realtimeSinceStartup - _tooltipChangedAt < TooltipShowDelaySeconds)
            {
                _tweens.Set(TooltipKey, 0f);
                return 0f;
            }

            return GetBinaryProgress(TooltipKey, visible, TooltipDuration, ScenarioUiEasing.EaseOut, false);
        }

        public string ResolveTooltip(string tooltip)
        {
            if (!string.IsNullOrEmpty(tooltip))
            {
                _tooltipLastSeenFrame = Time.frameCount;
                return tooltip;
            }

            if (!string.IsNullOrEmpty(_lastTooltip)
                && Time.frameCount - _tooltipLastSeenFrame <= 1)
            {
                return _lastTooltip;
            }

            return string.Empty;
        }

        public float GetModalDimAlpha(bool visible)
        {
            if (visible && !_modalVisibleLastFrame)
                _tweens.Set(ModalPanelKey, 0f);

            _modalVisibleLastFrame = visible;
            return GetBinaryProgress(ModalDimKey, visible, ModalDimDuration, ScenarioUiEasing.EaseOut, true) * 0.45f;
        }

        public float GetModalPanelProgress(bool visible)
        {
            float progress = GetBinaryProgress(ModalPanelKey, visible, ModalPanelDuration, ScenarioUiEasing.EaseOut, true);
            if (visible && _tweens.IsRunning(ModalPanelKey))
                TransitionActive = true;
            return progress;
        }

        public float GetHelpModalDimAlpha(bool visible)
        {
            return GetBinaryProgress(HelpModalDimKey, visible, ModalDimDuration, ScenarioUiEasing.EaseOut, true) * 0.36f;
        }

        public float GetHelpModalPanelProgress(bool visible)
        {
            return GetBinaryProgress(HelpModalPanelKey, visible, ModalPanelDuration, ScenarioUiEasing.EaseOut, true);
        }

        public float GetTutorialOverlayProgress(bool visible)
        {
            return GetBinaryProgress(TutorialOverlayKey, visible, PopupDuration, ScenarioUiEasing.PopupOut, true);
        }

        public float GetToastProgress(string status)
        {
            if (!string.Equals(_lastStatus, status, StringComparison.Ordinal))
            {
                _lastStatus = status;
                _statusChangedAt = Time.realtimeSinceStartup;
                if (!string.IsNullOrEmpty(status))
                    _tweens.Set(ToastKey, 0f);
            }

            bool visible = !string.IsNullOrEmpty(status)
                && Time.realtimeSinceStartup - _statusChangedAt <= ToastHoldSeconds;
            return GetBinaryProgress(ToastKey, visible, visible ? ToastInDuration : ToastOutDuration, ScenarioUiEasing.EaseOut, false);
        }

        public float GetBinaryProgress(string key, bool visible, float duration, ScenarioUiEasing easing, bool blocksWorldInput)
        {
            if (string.IsNullOrEmpty(key))
                return visible ? 1f : 0f;

            if (!_enabled)
                return visible ? 1f : 0f;

            _tweens.PlayFromCurrent(key, visible ? 1f : 0f, duration, easing, visible ? 1f : 0f);
            if (blocksWorldInput && _tweens.IsRunning(key))
                TransitionActive = true;
            return _tweens.GetValue(key, visible ? 1f : 0f);
        }

        public float GetPulseProgress(string key, string triggerSignature, float duration, ScenarioUiEasing easing)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(triggerSignature))
                return 0f;

            if (!_enabled)
                return 0f;

            string previous;
            if (!_pulseSignatures.TryGetValue(key, out previous) || !string.Equals(previous, triggerSignature, StringComparison.Ordinal))
            {
                _pulseSignatures[key] = triggerSignature;
                _tweens.Play(key, 1f, 0f, duration, easing);
            }

            return _tweens.GetValue(key, 0f);
        }

        private void PlayWindowOpen(WindowVisualState state)
        {
            state.Closing = false;
            if (!_enabled)
            {
                _tweens.Set(state.AlphaKey, 1f);
                _tweens.Set(state.ScaleKey, 1f);
                state.Alpha = 1f;
                state.Scale = 1f;
                return;
            }

            _tweens.Play(state.AlphaKey, 0f, 1f, WindowOpenDuration, ScenarioUiEasing.EaseOut);
            _tweens.Play(state.ScaleKey, 0.975f, 1f, WindowOpenDuration, ScenarioUiEasing.EaseOut);
            TransitionActive = true;
        }

        private void PlayWindowClose(WindowVisualState state)
        {
            if (state.Closing)
                return;

            state.Closing = true;
            if (!_enabled)
            {
                state.Alpha = 0f;
                state.Scale = 0.985f;
                state.Closing = false;
                _tweens.Set(state.AlphaKey, 0f);
                _tweens.Set(state.ScaleKey, 0.985f);
                return;
            }

            _tweens.Play(state.AlphaKey, _tweens.GetValue(state.AlphaKey, 1f), 0f, WindowCloseDuration, ScenarioUiEasing.EaseInOut);
            _tweens.Play(state.ScaleKey, _tweens.GetValue(state.ScaleKey, 1f), 0.985f, WindowCloseDuration, ScenarioUiEasing.EaseInOut);
            TransitionActive = true;
        }

        private WindowVisualState GetWindowState(string windowId)
        {
            WindowVisualState state;
            if (_windows.TryGetValue(windowId, out state))
                return state;

            state = new WindowVisualState(windowId);
            _windows.Add(windowId, state);
            return state;
        }

        private static bool ShouldAnimate(ScenarioAuthoringSettingsSnapshot settings)
        {
            if (settings == null)
                return true;

            return settings.GetBool("visuals.ui_animations", true)
                && string.Equals(settings.Get("shell.renderer_mode", "imgui"), "imgui", StringComparison.OrdinalIgnoreCase);
        }

        internal sealed class WindowVisualState
        {
            public readonly string Id;
            public readonly string AlphaKey;
            public readonly string ScaleKey;
            public ScenarioAuthoringShellWindowViewModel Window;
            public Rect LastRect;
            public bool Floating;
            public bool WasVisible;
            public bool VisibleThisFrame;
            public bool RegisteredThisFrame;
            public bool Closing;
            public float Alpha;
            public float Scale;

            public WindowVisualState(string id)
            {
                Id = id;
                AlphaKey = string.Concat("window:", id, ":alpha");
                ScaleKey = string.Concat("window:", id, ":scale");
                LastRect = RuntimeCompat.ZeroRect();
                Alpha = 1f;
                Scale = 1f;
            }
        }
    }
}
