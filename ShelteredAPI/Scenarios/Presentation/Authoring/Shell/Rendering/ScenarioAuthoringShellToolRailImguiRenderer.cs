using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Animation;
using ShelteredAPI.Scenarios.Presentation.UiKit.Frame;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private const float RailRestoreChipHeight = 30f;
        private const float RailRestoreChipGap = 6f;

        private Rect DrawToolRailCore(Rect contentRect, ScenarioAuthoringShellViewModel shell, ScenarioAuthoringState state)
        {
            return DrawToolRailCore(contentRect, shell, state, 0);
        }

        private Rect DrawToolRailCore(Rect contentRect, ScenarioAuthoringShellViewModel shell, ScenarioAuthoringState state, int restoreChipCount)
        {
            ScenarioAuthoringToolButtonViewModel[] buttons = shell != null ? shell.ToolButtons : null;
            int count = buttons != null ? buttons.Length : 0;
            float restoreReserve = restoreChipCount > 0
                ? (restoreChipCount * RailRestoreChipHeight) + (restoreChipCount * RailRestoreChipGap)
                : 0f;
            Rect rect = ScenarioAuthoringShellLayout.BuildToolRailRect(contentRect, count, restoreReserve);
            DrawChromePanel(rect, _rootPanelStyle);

            float buttonHeight;
            float buttonStep;
            ResolveToolRailButtonMetrics(rect, count, out buttonHeight, out buttonStep);
            DrawToolRailActiveIndicator(rect, state, buttons, buttonStep, buttonHeight);

            float y = rect.y + 12f;
            for (int i = 0; buttons != null && i < buttons.Length; i++)
            {
                if (y + buttonHeight > rect.yMax - 6f)
                    break;

                DrawToolRailButton(new Rect(rect.x + 10f, y, rect.width - 20f, buttonHeight), state, buttons[i]);
                y += buttonStep;
            }
            return rect;
        }

        private Rect DrawWorldToolRestoreChips(Rect contentRect, Rect railRect, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            List<ScenarioAuthoringShellWindowViewModel> collapsed = GetCollapsedWorldToolWindows(windows);
            if (collapsed.Count == 0)
                return RuntimeCompat.ZeroRect();

            Rect combined = RuntimeCompat.ZeroRect();
            for (int i = 0; i < collapsed.Count; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = collapsed[i];
                Rect chipRect = ScenarioAuthoringShellLayout.BuildRailRestoreChipRect(contentRect, railRect, i, collapsed.Count);
                DrawButton(chipRect, new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionWindowRestorePrefix + window.Id,
                    Label = "+ " + (window.Title ?? "Panel"),
                    Hint = "Restore the " + (window.Title ?? "panel") + " panel.",
                    Enabled = true
                }, false);

                combined = combined.width <= 0f || combined.height <= 0f ? chipRect : Union(combined, chipRect);
            }

            return combined;
        }

        private static Rect Union(Rect first, Rect second)
        {
            float xMin = Math.Min(first.xMin, second.xMin);
            float yMin = Math.Min(first.yMin, second.yMin);
            float xMax = Math.Max(first.xMax, second.xMax);
            float yMax = Math.Max(first.yMax, second.yMax);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static int CountCollapsedWorldToolWindows(ScenarioAuthoringShellWindowViewModel[] windows)
        {
            return GetCollapsedWorldToolWindows(windows).Count;
        }

        private static List<ScenarioAuthoringShellWindowViewModel> GetCollapsedWorldToolWindows(ScenarioAuthoringShellWindowViewModel[] windows)
        {
            List<ScenarioAuthoringShellWindowViewModel> collapsed = new List<ScenarioAuthoringShellWindowViewModel>();
            AddCollapsedWorldToolWindow(collapsed, windows, ScenarioAuthoringWindowIds.Inspector);
            AddCollapsedWorldToolWindow(collapsed, windows, ScenarioAuthoringWindowIds.BuildTools);
            return collapsed;
        }

        private static void AddCollapsedWorldToolWindow(
            List<ScenarioAuthoringShellWindowViewModel> collapsed,
            ScenarioAuthoringShellWindowViewModel[] windows,
            string windowId)
        {
            ScenarioAuthoringShellWindowViewModel window = FindWindow(windows, windowId);
            if (window == null || !window.Collapsed)
                return;

            if (string.Equals(windowId, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase)
                && IsEmptyInspector(window))
            {
                return;
            }

            collapsed.Add(window);
        }

        private static void ResolveToolRailButtonMetrics(Rect railRect, int buttonCount, out float buttonHeight, out float buttonStep)
        {
            const float regularButtonHeight = 48f;
            const float regularButtonStep = 56f;
            if (buttonCount <= 0)
            {
                buttonHeight = regularButtonHeight;
                buttonStep = regularButtonStep;
                return;
            }

            float available = Math.Max(40f, railRect.height - 24f);
            float compactGap = 4f;
            float compactHeight = (available - (compactGap * (buttonCount - 1))) / buttonCount;
            buttonHeight = Mathf.Clamp(compactHeight, 24f, regularButtonHeight);
            buttonStep = buttonHeight + (buttonHeight >= regularButtonHeight - 0.001f ? 8f : compactGap);
        }

        private void DrawToolRailActiveIndicator(
            Rect railRect,
            ScenarioAuthoringState state,
            ScenarioAuthoringToolButtonViewModel[] buttons,
            float buttonStep,
            float buttonHeight)
        {
            ScenarioAuthoringToolButtonViewModel activeButton = null;
            int activeIndex = -1;
            for (int i = 0; buttons != null && i < buttons.Length; i++)
            {
                if (state != null && buttons[i] != null && state.ActiveTool == buttons[i].Tool)
                {
                    activeButton = buttons[i];
                    activeIndex = i;
                    break;
                }
            }

            if (activeButton == null || activeIndex < 0)
                return;

            string activeKey = activeButton.Tool.ToString();
            float targetY = railRect.y + 12f + (activeIndex * buttonStep);
            if (!string.Equals(_toolRailActiveKey, activeKey, StringComparison.Ordinal))
            {
                _toolRailActiveKey = activeKey;
                if (_toolRailIndicatorY < 0f)
                    _toolRailIndicatorY = targetY;
            }

            float move = 1f - _animations.GetPulseProgress("toolrail.indicator.move", activeKey, 0.18f, ScenarioUiEasing.EaseOut);
            _toolRailIndicatorY = Mathf.Lerp(_toolRailIndicatorY, targetY, Mathf.Clamp01(move));
            float fade = _animations.GetBinaryProgress("toolrail.indicator.visible", true, 0.12f, ScenarioUiEasing.EaseOut, false);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.67f, 0.52f, 0.19f, 0.88f * fade);
            GUI.DrawTexture(new Rect(railRect.x + 4f, _toolRailIndicatorY + 4f, 4f, Math.Max(18f, buttonHeight - 8f)), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawToolRailButton(Rect rect, ScenarioAuthoringState state, ScenarioAuthoringToolButtonViewModel button)
        {
            if (button == null || button.Action == null)
                return;

            bool active = state != null && state.ActiveTool == button.Tool;
            GUIStyle style = active ? _activeButtonStyle : _buttonStyle;
            Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            bool hovered = rect.Contains(mouse);
            bool pressed = hovered && Event.current != null && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            float press = button.Action.Enabled ? _animations.GetButtonPressForAction(button.Action.Id, pressed) : 0f;
            Rect visualRect = press > 0.001f
                ? new Rect(rect.x + press, rect.y - press, rect.width, rect.height)
                : rect;
            RegisterTourTarget("tool:" + button.Tool, visualRect);
            if (GUI.Button(visualRect, new GUIContent(string.Empty, button.Action.Hint ?? string.Empty), style) && button.Action.Enabled)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(button.Action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }

            DrawButtonAnimationOverlay(visualRect, button.Action.Id, button.Action.Enabled, hovered, pressed);
            float labelHeight = visualRect.height < 28f ? 16f : 20f;
            float labelY = visualRect.y + Math.Max(3f, (visualRect.height - labelHeight) * 0.5f);
            GUIStyle labelStyle = active ? _textStyle : _mutedTextStyle;
            Rect labelRect = new Rect(visualRect.x + 8f, labelY, visualRect.width - 16f, labelHeight);
            GUI.Label(labelRect, ShortenToFit(button.Label ?? string.Empty, labelRect.width, labelStyle), labelStyle);
        }

    }
}
