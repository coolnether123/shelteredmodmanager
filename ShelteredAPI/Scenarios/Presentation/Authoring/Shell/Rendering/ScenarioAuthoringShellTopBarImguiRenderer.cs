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
        private Rect DrawTopBarCore(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            DrawChromePanel(rect, _rootPanelStyle);
            const float primaryRowY = 10f;
            const float primaryRowHeight = 36f;
            const float secondaryRowY = 54f;
            const float secondaryRowHeight = 30f;

            Rect brandRect = new Rect(rect.x + 18f, rect.y + 9f, 220f, 78f);
            GUI.Label(new Rect(brandRect.x, brandRect.y, brandRect.width, 30f), "SHELTERED", _titleStyle);
            GUI.Label(new Rect(brandRect.x, brandRect.y + 31f, brandRect.width, 20f), "Scenario Workshop", _smallTitleStyle);
            if (shell != null && !string.IsNullOrEmpty(shell.Subtitle))
                GUI.Label(new Rect(brandRect.x, brandRect.y + 56f, brandRect.width, 18f), ShortenToFit(shell.Subtitle, brandRect.width, _mutedTextStyle), _mutedTextStyle);

            float primaryRowLeft = brandRect.xMax + 20f;
            float actionRight = rect.xMax - 10f;
            float toolbarWidth = MeasureTopBarActionsWidth(shell.ToolbarActions);
            float toolbarX = Math.Max(primaryRowLeft, actionRight - toolbarWidth);
            Rect windowMenuButtonRect = DrawTopBarWindowAction(
                new Rect(primaryRowLeft, rect.y + secondaryRowY, actionRight - primaryRowLeft, secondaryRowHeight),
                shell);

            float primaryTabsRight = Math.Max(primaryRowLeft, toolbarX - 10f);
            float tabX = primaryRowLeft;
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                if (IsChildStageTab(tab))
                    continue;

                float tabWidth = Mathf.Clamp(MeasureButtonWidth(tab, true, 30f), 92f, 170f);
                if (tabX + tabWidth > primaryTabsRight)
                    break;
                Rect tabRect = new Rect(tabX, rect.y + primaryRowY, tabWidth, primaryRowHeight);
                DrawButton(tabRect, tab, true);
                tabX = tabRect.xMax + 2f;
            }

            DrawTopBarToolbarActions(
                new Rect(toolbarX, rect.y + primaryRowY + 3f, actionRight - toolbarX, secondaryRowHeight),
                shell);

            float childTabsRight = windowMenuButtonRect.width > 0f ? windowMenuButtonRect.x - 10f : actionRight;
            Rect childTabsRect = new Rect(
                primaryRowLeft,
                rect.y + secondaryRowY,
                Math.Max(80f, childTabsRight - primaryRowLeft),
                secondaryRowHeight);
            float childX = childTabsRect.x;
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                if (!IsChildStageTab(tab))
                    continue;

                ScenarioAuthoringInspectorAction displayTab = CloneWithLabel(tab, CleanChildStageLabel(tab.Label));
                float width = Mathf.Clamp(MeasureButtonWidth(displayTab, true, 26f), 94f, 122f);
                Rect tabRect = new Rect(childX, childTabsRect.y, width, childTabsRect.height);
                if (tabRect.xMax > childTabsRect.xMax)
                    break;
                DrawButton(tabRect, displayTab, true);
                childX = tabRect.xMax + 2f;
            }

            return windowMenuButtonRect;
        }

        private void DrawTopBarToolbarActions(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            float x = rect.x;
            for (int i = 0; shell.ToolbarActions != null && i < shell.ToolbarActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = shell.ToolbarActions[i];
                if (action == null)
                    continue;

                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 96f, 126f);
                Rect actionRect = new Rect(x, rect.y, width, rect.height);
                if (actionRect.xMax > rect.xMax)
                    break;
                DrawButton(actionRect, action, false);
                x = actionRect.xMax + 4f;
            }
        }

        private Rect DrawTopBarWindowAction(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            for (int i = 0; shell.LayoutActions != null && i < shell.LayoutActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = shell.LayoutActions[i];
                if (!IsWindowMenuAction(action))
                    continue;

                ScenarioAuthoringInspectorAction displayAction = _windowMenuOpen ? CloneEmphasized(action) : action;
                Rect actionRect = new Rect(rect.xMax - 106f, rect.y, 106f, rect.height);
                DrawButton(actionRect, displayAction, false);
                return actionRect;
            }

            return RuntimeCompat.ZeroRect();
        }

        private float MeasureTopBarActionsWidth(ScenarioAuthoringInspectorAction[] actions)
        {
            float width = 0f;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;

                width += Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 96f, 126f);
                if (i + 1 < actions.Length)
                    width += 4f;
            }

            return width;
        }

    }
}
