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
        private Rect DrawToolRailCore(Rect contentRect, ScenarioAuthoringShellViewModel shell, ScenarioAuthoringState state)
        {
            ScenarioAuthoringToolButtonViewModel[] buttons = shell != null ? shell.ToolButtons : null;
            int count = buttons != null ? buttons.Length : 0;
            Rect rect = ScenarioAuthoringShellLayout.BuildToolRailRect(contentRect, count);
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
            buttonHeight = Mathf.Clamp(compactHeight, 26f, regularButtonHeight);
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
            if (GUI.Button(visualRect, new GUIContent(string.Empty, button.Action.Hint ?? string.Empty), style) && button.Action.Enabled)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(button.Action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }

            DrawButtonAnimationOverlay(visualRect, button.Action.Id, button.Action.Enabled, hovered, pressed);
            float labelY = visualRect.y + Math.Max(4f, (visualRect.height - 20f) * 0.5f);
            GUI.Label(new Rect(visualRect.x + 8f, labelY, visualRect.width - 16f, 20f), button.Label ?? string.Empty, active ? _textStyle : _mutedTextStyle);
        }

    }
}
