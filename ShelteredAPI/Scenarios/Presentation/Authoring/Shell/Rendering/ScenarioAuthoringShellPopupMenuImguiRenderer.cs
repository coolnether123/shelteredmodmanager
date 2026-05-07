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
        private void DrawContextMenuCore(Rect rect, ScenarioAuthoringContextMenuModel menu)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Menu);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            GUILayout.Label(menu.Title ?? "Context", _sectionTitleStyle);
            if (!string.IsNullOrEmpty(menu.Detail))
                GUILayout.Label(menu.Detail, _mutedTextStyle);
            GUILayout.Space(4f);
            for (int i = 0; menu.Actions != null && i < menu.Actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = menu.Actions[i];
                if (action == null)
                    continue;

                Rect buttonRect = GUILayoutUtility.GetRect(rect.width - 24f, 24f, GUILayout.Height(24f));
                DrawButton(buttonRect, action, false);
            }
            GUILayout.EndArea();
        }

        private Rect BuildPopupRectCore(ScenarioAuthoringContextMenuModel menu, float width, float height, Rect hudReserveRect)
        {
            float rectWidth = 220f;
            float rectHeight = 54f + ((menu.Actions != null ? menu.Actions.Length : 0) * 28f);
            Rect rect = new Rect(
                Mathf.Clamp(menu.AnchorX + 16f, Margin, width - rectWidth - Margin),
                Mathf.Clamp(menu.AnchorY + 16f, Margin, height - rectHeight - Margin),
                rectWidth,
                rectHeight);
            return ClampAwayFromHud(rect, width, height, hudReserveRect);
        }

        private Rect BuildWindowMenuRectCore(Rect buttonRect, ScenarioAuthoringInspectorAction[] actions, float width, float height, Rect hudReserveRect)
        {
            float rectWidth = 220f;
            for (int i = 0; actions != null && i < actions.Length; i++)
                rectWidth = Math.Max(rectWidth, MeasureButtonWidth(actions[i], false, 26f) + 24f);

            rectWidth = Mathf.Clamp(rectWidth, 220f, 320f);
            float rectHeight = 16f + ((actions != null ? actions.Length : 0) * 28f);
            Rect rect = new Rect(
                Mathf.Clamp(buttonRect.x, Margin, width - rectWidth - Margin),
                Mathf.Clamp(buttonRect.yMax + 4f, Margin, height - rectHeight - Margin),
                rectWidth,
                rectHeight);
            return ClampAwayFromHud(rect, width, height, hudReserveRect);
        }

        private void DrawWindowMenuCore(Rect rect, ScenarioAuthoringInspectorAction[] actions)
        {
            GUI.Box(rect, GUIContent.none, _uiContext.Styles.Menu);
            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                Rect buttonRect = GUILayoutUtility.GetRect(rect.width - 24f, 24f, GUILayout.Height(24f));
                DrawButton(buttonRect, action, false);
            }
            GUILayout.EndArea();
        }

        private void DrawButton(Rect rect, ScenarioAuthoringInspectorAction action, bool tab)
        {
            if (action == null)
                return;

            GUIContent content = new GUIContent(action.Label ?? string.Empty, action.Hint ?? string.Empty);

            if (IsWindowMenuAction(action))
            {
                GUI.enabled = action.Enabled;
                GUIStyle menuStyle = tab
                    ? (action.Emphasized ? _activeTabStyle : _tabStyle)
                    : (action.Emphasized ? _activeButtonStyle : _buttonStyle);
                if (GUI.Button(rect, content, menuStyle) && action.Enabled)
                {
                    _windowMenuOpen = !_windowMenuOpen;
                    if (Event.current != null)
                        Event.current.Use();
                }
                GUI.enabled = true;
                return;
            }

            GUI.enabled = action.Enabled;
            GUIStyle style = tab
                ? (action.Emphasized ? _activeTabStyle : _tabStyle)
                : (action.Emphasized ? _activeButtonStyle : _buttonStyle);
            if (GUI.Button(rect, content, style) && action.Enabled)
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(action.Id);
                if (Event.current != null)
                    Event.current.Use();
            }
            GUI.enabled = true;
        }
    }
}
