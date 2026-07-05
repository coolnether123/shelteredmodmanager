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
            bool compact = IsCompactTopBar(rect);
            float primaryRowY = compact ? 42f : 10f;
            float primaryRowHeight = compact ? 24f : 36f;
            float secondaryRowY = compact ? 68f : 54f;
            float secondaryRowHeight = compact ? 24f : 30f;

            Rect brandRect = compact
                ? new Rect(rect.x + 14f, rect.y + 8f, 206f, 30f)
                : new Rect(rect.x + 18f, rect.y + 9f, 220f, 78f);
            GUI.Label(new Rect(brandRect.x, brandRect.y, brandRect.width, 30f), "SHELTERED", _titleStyle);
            if (!compact)
                GUI.Label(new Rect(brandRect.x, brandRect.y + 31f, brandRect.width, 20f), "Scenario Workshop", _smallTitleStyle);
            if (!compact && shell != null && !string.IsNullOrEmpty(shell.Subtitle))
                GUI.Label(new Rect(brandRect.x, brandRect.y + 56f, brandRect.width, 18f), ShortenToFit(shell.Subtitle, brandRect.width, _mutedTextStyle), _mutedTextStyle);

            float primaryRowLeft = compact ? rect.x + 12f : brandRect.xMax + 20f;
            float actionRight = rect.xMax - 10f;
            float toolbarWidth = MeasureTopBarActionsWidth(shell.ToolbarActions, compact);
            float toolbarX = Math.Max(primaryRowLeft, actionRight - toolbarWidth);
            Rect windowMenuButtonRect = DrawTopBarWindowAction(
                compact
                    ? new Rect(rect.xMax - 94f, rect.y + 10f, 84f, 28f)
                    : new Rect(primaryRowLeft, rect.y + secondaryRowY, actionRight - primaryRowLeft, secondaryRowHeight),
                shell);

            float primaryTabsRight = compact ? rect.xMax - 12f : Math.Max(primaryRowLeft, toolbarX - 10f);
            float tabX = primaryRowLeft;
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                if (IsChildStageTab(tab))
                    continue;

                ScenarioAuthoringInspectorAction displayTab = compact ? CloneWithLabel(tab, CompactStageLabel(tab.Label)) : tab;
                float tabWidth = ResolvePrimaryStageTabWidth(displayTab, compact);
                if (tabX + tabWidth > primaryTabsRight)
                    break;
                Rect tabRect = new Rect(tabX, rect.y + primaryRowY, tabWidth, primaryRowHeight);
                DrawButton(tabRect, displayTab, true);
                tabX = tabRect.xMax + 2f;
            }

            DrawTopBarToolbarActions(
                compact
                    ? new Rect(Math.Min(rect.x + 228f, rect.xMax - 188f), rect.y + 10f, Math.Max(0f, windowMenuButtonRect.x - Math.Min(rect.x + 228f, rect.xMax - 188f) - 8f), 28f)
                    : new Rect(toolbarX, rect.y + primaryRowY + 3f, actionRight - toolbarX, secondaryRowHeight),
                shell,
                compact);

            float childTabsRight = compact ? rect.xMax - 12f : (windowMenuButtonRect.width > 0f ? windowMenuButtonRect.x - 10f : actionRight);
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
                float width = ResolveChildStageTabWidth(displayTab, compact);
                Rect tabRect = new Rect(childX, childTabsRect.y, width, childTabsRect.height);
                if (tabRect.xMax > childTabsRect.xMax)
                    break;
                DrawButton(tabRect, displayTab, true);
                childX = tabRect.xMax + 2f;
            }

            return windowMenuButtonRect;
        }

        private void DrawTopBarToolbarActions(Rect rect, ScenarioAuthoringShellViewModel shell, bool compact)
        {
            float x = rect.x;
            for (int i = 0; shell.ToolbarActions != null && i < shell.ToolbarActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = shell.ToolbarActions[i];
                if (action == null)
                    continue;
                if (compact && IsLowPriorityTopBarAction(action))
                    continue;

                float width = ResolveToolbarActionWidth(action, compact);
                Rect actionRect = new Rect(x, rect.y, width, rect.height);
                if (actionRect.xMax > rect.xMax)
                    break;
                DrawButton(actionRect, action, false);
                x = actionRect.xMax + (compact ? 3f : 4f);
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
                Rect actionRect = rect.width <= 90f
                    ? rect
                    : new Rect(rect.xMax - 106f, rect.y, 106f, rect.height);
                DrawButton(actionRect, displayAction, false);
                return actionRect;
            }

            return RuntimeCompat.ZeroRect();
        }

        private float MeasureTopBarActionsWidth(ScenarioAuthoringInspectorAction[] actions)
        {
            return MeasureTopBarActionsWidth(actions, false);
        }

        private float MeasureTopBarActionsWidth(ScenarioAuthoringInspectorAction[] actions, bool compact)
        {
            float width = 0f;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null)
                    continue;
                if (compact && IsLowPriorityTopBarAction(action))
                    continue;

                width += ResolveToolbarActionWidth(action, compact);
                if (i + 1 < actions.Length)
                    width += compact ? 3f : 4f;
            }

            return width;
        }

        private static bool IsCompactTopBar(Rect rect)
        {
            return rect.width < 1040f;
        }

        private float ResolvePrimaryStageTabWidth(ScenarioAuthoringInspectorAction action, bool compact)
        {
            return compact
                ? Mathf.Clamp(MeasureButtonWidth(action, true, 14f), 48f, 72f)
                : Mathf.Clamp(MeasureButtonWidth(action, true, 30f), 92f, 170f);
        }

        private float ResolveChildStageTabWidth(ScenarioAuthoringInspectorAction action, bool compact)
        {
            return compact
                ? Mathf.Clamp(MeasureButtonWidth(action, true, 14f), 64f, 82f)
                : Mathf.Clamp(MeasureButtonWidth(action, true, 26f), 94f, 122f);
        }

        private float ResolveToolbarActionWidth(ScenarioAuthoringInspectorAction action, bool compact)
        {
            return compact
                ? Mathf.Clamp(MeasureButtonWidth(action, false, 16f), 78f, 92f)
                : Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 96f, 126f);
        }

        private static bool IsLowPriorityTopBarAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellOpenHelp, StringComparison.Ordinal)
                    || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellOpenSettings, StringComparison.Ordinal));
        }

        private static string CompactStageLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return string.Empty;
            if (string.Equals(label, "Supplies", StringComparison.OrdinalIgnoreCase))
                return "Supply";
            if (string.Equals(label, "Timeline", StringComparison.OrdinalIgnoreCase))
                return "Time";
            if (string.Equals(label, "Publish", StringComparison.OrdinalIgnoreCase))
                return "Pub";

            return label;
        }

    }
}
