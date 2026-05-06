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
        private void DrawWorkspaceTabsCore(Rect rect, string activeId, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            GUI.Box(rect, GUIContent.none, _headerStyle);
            ScenarioAuthoringShellWindowViewModel[] tabWindows = GetWorkspaceTabWindows(windows);
            if (tabWindows.Length == 0)
                return;

            float tabWidth = (rect.width - 8f) / tabWindows.Length;
            for (int i = 0; i < tabWindows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = tabWindows[i];
                Rect tabRect = new Rect(rect.x + 4f + (tabWidth * i), rect.y + 3f, tabWidth - 4f, rect.height - 6f);
                bool isActive = string.Equals(window.Id, activeId, StringComparison.OrdinalIgnoreCase);
                ScenarioAuthoringInspectorAction action = new ScenarioAuthoringInspectorAction
                {
                    Id = ScenarioAuthoringActionIds.ActionWindowTogglePrefix + window.Id,
                    Label = window.Title,
                    Hint = "Open the " + window.Title + " workspace.",
                    Enabled = true,
                    Emphasized = isActive
                };
                DrawButton(tabRect, action, true);
            }
        }

    }
}
