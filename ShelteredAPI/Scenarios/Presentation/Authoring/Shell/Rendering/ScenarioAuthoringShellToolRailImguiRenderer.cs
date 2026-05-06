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
            float height = Math.Min(560f, Math.Max(112f, 16f + (count * 78f)));
            Rect rect = new Rect(contentRect.x + 4f, contentRect.y + 26f, ToolRailWidth, height);
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);

            float y = rect.y + 10f;
            for (int i = 0; buttons != null && i < buttons.Length; i++)
            {
                DrawToolRailButton(new Rect(rect.x + 8f, y, rect.width - 16f, 72f), state, buttons[i]);
                y += 78f;
                if (y + 72f > rect.yMax)
                    break;
            }
            return rect;
        }

        private void DrawToolRailButton(Rect rect, ScenarioAuthoringState state, ScenarioAuthoringToolButtonViewModel button)
        {
            if (button == null || button.Action == null)
                return;

            bool active = state != null && state.ActiveTool == button.Tool;
            GUIStyle style = active ? _activeButtonStyle : _buttonStyle;
            if (GUI.Button(rect, new GUIContent(string.Empty, button.Action.Hint ?? string.Empty), style))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(button.Action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }

            GUI.Label(new Rect(rect.x + 2f, rect.y + 9f, rect.width - 4f, 22f), button.IconText ?? string.Empty, _sectionTitleStyle);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 42f, rect.width - 4f, rect.height - 44f), button.Label ?? string.Empty, _mutedTextStyle);
        }

    }
}
