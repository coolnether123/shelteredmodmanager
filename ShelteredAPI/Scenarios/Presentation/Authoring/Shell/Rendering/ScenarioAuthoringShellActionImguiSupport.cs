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
        private static bool IsWindowMenuAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellToggleWindowMenu, StringComparison.Ordinal);
        }

        private ScenarioAuthoringInspectorAction[] GetHeaderActions(ScenarioAuthoringInspectorAction[] actions, bool chromeOnly)
        {
            if (actions == null || actions.Length == 0)
                return new ScenarioAuthoringInspectorAction[0];

            List<ScenarioAuthoringInspectorAction> filtered = new List<ScenarioAuthoringInspectorAction>();
            for (int i = 0; i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                bool isChrome = action.Id != null
                    && (action.Id.StartsWith(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix, StringComparison.Ordinal)
                        || action.Id.StartsWith(ScenarioAuthoringActionIds.ActionWindowTogglePrefix, StringComparison.Ordinal));
                if (isChrome == chromeOnly)
                    filtered.Add(action);
            }

            return filtered.ToArray();
        }

        private float MeasureButtonWidth(ScenarioAuthoringInspectorAction action, bool tab, float extraPadding)
        {
            GUIStyle style = tab
                ? (action != null && action.Emphasized ? _activeTabStyle : _tabStyle)
                : (action != null && action.Emphasized ? _activeButtonStyle : _buttonStyle);
            Vector2 size = style.CalcSize(new GUIContent(action != null ? action.Label ?? string.Empty : string.Empty));
            return size.x + extraPadding;
        }
    }
}
