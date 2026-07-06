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
            bool compact = IsCompactTopBar(rect, shell);
            float primaryRowY = compact ? 42f : 10f;
            float primaryRowHeight = compact ? 24f : 36f;
            float utilityRowY = compact ? 10f : 54f;
            float utilityRowHeight = compact ? 28f : 30f;

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
            Rect windowMenuButtonRect = DrawTopBarWindowAction(
                compact
                    ? new Rect(rect.xMax - 122f, rect.y + 10f, 112f, 28f)
                    : new Rect(primaryRowLeft, rect.y + utilityRowY, actionRight - primaryRowLeft, utilityRowHeight),
                shell);

            float saveWidth = MeasureTopBarActionsWidth(shell.ToolbarActions, compact);
            float saveRight = windowMenuButtonRect.width > 0f ? windowMenuButtonRect.x - (compact ? 4f : 8f) : actionRight;
            float saveX = Math.Max(primaryRowLeft, saveRight - saveWidth);
            Rect saveRect = new Rect(saveX, rect.y + utilityRowY, Math.Max(0f, saveRight - saveX), utilityRowHeight);
            DrawTopBarToolbarActions(saveRect, shell, compact);

            float primaryTabsRight = compact ? rect.xMax - 12f : Math.Max(primaryRowLeft, saveX - 12f);
            float tabX = primaryRowLeft;
            float finishStart = 0f;
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                if (IsChildStageTab(tab) || IsFinishStageTab(tab))
                    continue;

                ScenarioAuthoringInspectorAction displayTab = compact ? CloneWithLabel(tab, CompactStageLabel(tab.Label)) : tab;
                float tabWidth = ResolvePrimaryStageTabWidth(displayTab, compact);
                if (tabX + tabWidth > primaryTabsRight)
                    break;
                Rect tabRect = new Rect(tabX, rect.y + primaryRowY, tabWidth, primaryRowHeight);
                DrawButton(tabRect, displayTab, true);
                tabX = tabRect.xMax + 2f;
            }

            finishStart = tabX + (compact ? 4f : 10f);
            if (!compact && finishStart + 1f < primaryTabsRight)
                ScenarioUiWidgets.DrawVerticalDivider(new Rect(finishStart - 6f, rect.y + primaryRowY + 5f, 1f, primaryRowHeight - 10f), _uiContext.Styles);
            tabX = finishStart;
            for (int i = 0; shell.Tabs != null && i < shell.Tabs.Length; i++)
            {
                ScenarioAuthoringInspectorAction tab = shell.Tabs[i];
                if (!IsFinishStageTab(tab))
                    continue;

                ScenarioAuthoringInspectorAction displayTab = compact ? CloneWithLabel(tab, CompactStageLabel(tab.Label)) : tab;
                float width = ResolvePrimaryStageTabWidth(displayTab, compact);
                Rect tabRect = new Rect(tabX, rect.y + primaryRowY, width, primaryRowHeight);
                if (tabRect.xMax > primaryTabsRight)
                    break;
                DrawButton(tabRect, displayTab, true);
                tabX = tabRect.xMax + 2f;
            }

            if (_snapshot != null && IsWorldStage(_snapshot.State))
            {
                Rect worldControlsRect = compact
                    ? new Rect(primaryRowLeft, rect.y + 70f, primaryTabsRight - primaryRowLeft, 20f)
                    : new Rect(primaryRowLeft, rect.y + 56f, Math.Max(0f, saveX - primaryRowLeft - 12f), 24f);
                DrawWorldSurfaceControls(worldControlsRect, shell, compact);
            }

            return windowMenuButtonRect;
        }

        private void DrawWorldSurfaceControls(Rect rect, ScenarioAuthoringShellViewModel shell, bool compact)
        {
            if (rect.width <= 80f || rect.height <= 0f)
                return;

            float chipWidth = compact ? 54f : 74f;
            GUI.Label(new Rect(rect.x, rect.y + 2f, chipWidth, rect.height - 2f), compact ? "World" : "World", _mutedTextStyle);
            float x = rect.x + chipWidth + 4f;
            for (int i = 0; shell != null && shell.WorldSubstageActions != null && i < shell.WorldSubstageActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = shell.WorldSubstageActions[i];
                ScenarioAuthoringInspectorAction displayAction = compact ? CloneWithLabel(action, CleanChildStageLabel(CompactStageLabel(action.Label))) : CloneWithLabel(action, CleanChildStageLabel(action.Label));
                float width = ResolveChildStageTabWidth(displayAction, compact);
                if (x + width > rect.xMax)
                    break;

                DrawButton(new Rect(x, rect.y, width, rect.height), displayAction, true);
                x += width + 3f;
            }
        }

        private static bool IsWorldStage(ScenarioAuthoringState state)
        {
            return state != null
                && (state.ActiveStage == ScenarioStageKind.Bunker
                    || state.ActiveStage == ScenarioStageKind.BunkerBackground
                    || state.ActiveStage == ScenarioStageKind.BunkerSurface
                    || state.ActiveStage == ScenarioStageKind.BunkerInside);
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

                ScenarioAuthoringInspectorAction displayAction = compact ? CloneWithLabel(action, CompactToolbarLabel(action)) : action;
                float width = ResolveToolbarActionWidth(displayAction, compact);
                Rect actionRect = new Rect(x, rect.y, width, rect.height);
                if (actionRect.xMax > rect.xMax)
                    break;
                DrawButton(actionRect, displayAction, false);
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
                float width = Mathf.Clamp(MeasureButtonWidth(action, false, 22f), 104f, 132f);
                Rect actionRect = rect.width <= width
                    ? rect
                    : new Rect(rect.xMax - width, rect.y, width, rect.height);
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

        private static bool IsCompactTopBar(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            return rect.width < 1280f;
        }

        private static bool IsCompactTopBar(Rect rect)
        {
            return IsCompactTopBar(rect, null);
        }

        private float ResolvePrimaryStageTabWidth(ScenarioAuthoringInspectorAction action, bool compact)
        {
            return compact
                ? ResolveCompactPrimaryStageTabWidth(action)
                : Mathf.Clamp(MeasureButtonWidth(action, true, 30f), 96f, 196f);
        }

        private static float ResolveCompactPrimaryStageTabWidth(ScenarioAuthoringInspectorAction action)
        {
            string label = action != null ? action.Label : null;
            if (string.Equals(label, "World", StringComparison.OrdinalIgnoreCase))
                return 74f;
            if (string.Equals(label, "Story", StringComparison.OrdinalIgnoreCase))
                return 70f;
            if (string.Equals(label, "Home", StringComparison.OrdinalIgnoreCase))
                return 64f;
            if (string.Equals(label, "Cast", StringComparison.OrdinalIgnoreCase))
                return 60f;
            if (string.Equals(label, "Time", StringComparison.OrdinalIgnoreCase))
                return 60f;
            if (string.Equals(label, "Test", StringComparison.OrdinalIgnoreCase))
                return 60f;
            if (string.Equals(label, "Map", StringComparison.OrdinalIgnoreCase))
                return 54f;
            if (string.Equals(label, "Pub", StringComparison.OrdinalIgnoreCase))
                return 54f;
            if (string.Equals(label, "Sup", StringComparison.OrdinalIgnoreCase))
                return 56f;

            return 56f;
        }

        private float ResolveChildStageTabWidth(ScenarioAuthoringInspectorAction action, bool compact)
        {
            return compact
                ? Mathf.Clamp(MeasureButtonWidth(action, true, 14f), 62f, 96f)
                : Mathf.Clamp(MeasureButtonWidth(action, true, 26f), 96f, 148f);
        }

        private float ResolveToolbarActionWidth(ScenarioAuthoringInspectorAction action, bool compact)
        {
            return compact
                ? Mathf.Clamp(MeasureButtonWidth(action, false, 16f), 76f, 108f)
                : Mathf.Clamp(MeasureButtonWidth(action, false, 24f), 104f, 156f);
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
            if (string.Equals(label, "Home", StringComparison.OrdinalIgnoreCase))
                return "H";
            if (string.Equals(label, "World", StringComparison.OrdinalIgnoreCase))
                return "W";
            if (string.Equals(label, "Cast", StringComparison.OrdinalIgnoreCase))
                return "C";
            if (string.Equals(label, "Supplies", StringComparison.OrdinalIgnoreCase))
                return "Sup";
            if (string.Equals(label, "Timeline", StringComparison.OrdinalIgnoreCase))
                return "Tm";
            if (string.Equals(label, "Map", StringComparison.OrdinalIgnoreCase))
                return "M";
            if (string.Equals(label, "Publish", StringComparison.OrdinalIgnoreCase))
                return "P";
            if (string.Equals(label, "Backdrop", StringComparison.OrdinalIgnoreCase))
                return "Back";
            if (string.Equals(label, "Inside", StringComparison.OrdinalIgnoreCase))
                return "Inside";

            return label;
        }

        private static string CompactToolbarLabel(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Label))
                return string.Empty;
            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionSave, StringComparison.Ordinal))
                return "Save";
            return action.Label;
        }

        private static bool IsFinishStageTab(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id))
                return false;

            return string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Test, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Publish, StringComparison.Ordinal);
        }

    }
}
