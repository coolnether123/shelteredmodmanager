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
            DrawMeasuredStageTabs(new Rect(primaryRowLeft, rect.y + primaryRowY, Math.Max(0f, primaryTabsRight - primaryRowLeft), primaryRowHeight), shell, compact);

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
                ScenarioAuthoringInspectorAction displayAction = CloneWithLabel(action, CleanChildStageLabel(action.Label));
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

                float width = ResolveToolbarActionWidth(action, compact);
                Rect actionRect = new Rect(x, rect.y, width, rect.height);
                if (actionRect.xMax > rect.xMax)
                    break;
                DrawButton(actionRect, action, false);
                x = actionRect.xMax + (compact ? 3f : 4f);
            }
        }

        private void DrawMeasuredStageTabs(Rect rect, ScenarioAuthoringShellViewModel shell, bool compact)
        {
            _topBarOverflowTabs = new ScenarioAuthoringInspectorAction[0];
            _topBarMoreButtonRect = RuntimeCompat.ZeroRect();
            _topBarMoreMenuRect = RuntimeCompat.ZeroRect();
            if (rect.width <= 0f)
                return;

            List<StageTabLayout> tabs = BuildMeasuredStageTabList(shell != null ? shell.Tabs : null, compact);
            if (tabs.Count == 0)
                return;

            float moreWidth = Mathf.Clamp(ScenarioUiMeasuredLabel.Width("More >", _buttonStyle, 20f), 74f, 104f);
            int visibleCount = ResolveVisibleStageTabCount(tabs, rect.width, moreWidth);
            bool overflow = visibleCount < tabs.Count;
            float availableRight = overflow ? rect.xMax - moreWidth - 4f : rect.xMax;
            float x = rect.x;
            bool drewMain = false;
            for (int i = 0; i < visibleCount; i++)
            {
                StageTabLayout tab = tabs[i];
                if (tab.Finish && drewMain && x + 8f < availableRight)
                {
                    ScenarioUiWidgets.DrawVerticalDivider(new Rect(x + 2f, rect.y + 5f, 1f, rect.height - 10f), _uiContext.Styles);
                    x += 10f;
                }

                Rect tabRect = new Rect(x, rect.y, tab.Width, rect.height);
                if (tabRect.xMax > availableRight)
                    break;
                DrawButton(tabRect, tab.Action, true);
                RegisterTopBarActionAliases(tab.Action, tabRect);
                x = tabRect.xMax + 2f;
                if (!tab.Finish)
                    drewMain = true;
            }

            if (!overflow)
            {
                _topBarMoreMenuOpen = false;
                return;
            }

            List<ScenarioAuthoringInspectorAction> overflowTabs = new List<ScenarioAuthoringInspectorAction>();
            for (int i = visibleCount; i < tabs.Count; i++)
                overflowTabs.Add(tabs[i].Action);
            _topBarOverflowTabs = overflowTabs.ToArray();

            _topBarMoreButtonRect = new Rect(rect.xMax - moreWidth, rect.y, moreWidth, rect.height);
            ScenarioAuthoringInspectorAction moreAction = new ScenarioAuthoringInspectorAction
            {
                Id = "shell.stage.more",
                Label = "More >",
                Hint = "Show remaining stage tabs.",
                Enabled = true,
                Emphasized = _topBarMoreMenuOpen
            };
            DrawTopBarMoreButton(_topBarMoreButtonRect, moreAction);
            if (_topBarMoreMenuOpen)
                DrawTopBarMoreMenu(_topBarMoreButtonRect, _topBarOverflowTabs);
        }

        private List<StageTabLayout> BuildMeasuredStageTabList(ScenarioAuthoringInspectorAction[] actions, bool compact)
        {
            List<StageTabLayout> result = new List<StageTabLayout>();
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (IsChildStageTab(action))
                    continue;

                result.Add(new StageTabLayout
                {
                    Action = action,
                    Finish = IsFinishStageTab(action),
                    Width = ResolvePrimaryStageTabWidth(action, compact)
                });
            }

            result.Sort(delegate(StageTabLayout left, StageTabLayout right)
            {
                if (left.Finish == right.Finish)
                    return 0;
                return left.Finish ? 1 : -1;
            });
            return result;
        }

        private static int ResolveVisibleStageTabCount(List<StageTabLayout> tabs, float availableWidth, float moreWidth)
        {
            float fullWidth = MeasureStageTabRunWidth(tabs, tabs.Count);
            if (fullWidth <= availableWidth)
                return tabs.Count;

            float limit = Math.Max(0f, availableWidth - moreWidth - 4f);
            int visible = 0;
            while (visible < tabs.Count)
            {
                float width = MeasureStageTabRunWidth(tabs, visible + 1);
                if (width > limit)
                    break;
                visible++;
            }

            return Math.Max(0, visible);
        }

        private static float MeasureStageTabRunWidth(List<StageTabLayout> tabs, int count)
        {
            float width = 0f;
            bool drewMain = false;
            for (int i = 0; i < count && i < tabs.Count; i++)
            {
                StageTabLayout tab = tabs[i];
                if (i > 0)
                    width += 2f;
                if (tab.Finish && drewMain)
                    width += 10f;
                width += tab.Width;
                if (!tab.Finish)
                    drewMain = true;
            }

            return width;
        }

        private void DrawTopBarMoreButton(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            if (GUI.Button(rect, new GUIContent(action.Label, action.Hint), _topBarMoreMenuOpen ? _activeButtonStyle : _buttonStyle))
            {
                _topBarMoreMenuOpen = !_topBarMoreMenuOpen;
                if (Event.current != null)
                    Event.current.Use();
            }
        }

        private void DrawTopBarMoreMenu(Rect anchor, ScenarioAuthoringInspectorAction[] overflowTabs)
        {
            float width = 180f;
            for (int i = 0; overflowTabs != null && i < overflowTabs.Length; i++)
                width = Math.Max(width, MeasureButtonWidth(overflowTabs[i], false, 26f));
            width = Mathf.Clamp(width, 180f, 260f);
            float height = 16f + ((overflowTabs != null ? overflowTabs.Length : 0) * 30f);
            _topBarMoreMenuRect = new Rect(anchor.xMax - width, anchor.yMax + 4f, width, height);
            GUI.Box(_topBarMoreMenuRect, GUIContent.none, _uiContext.Styles.Menu);
            GUILayout.BeginArea(new Rect(_topBarMoreMenuRect.x + 8f, _topBarMoreMenuRect.y + 8f, _topBarMoreMenuRect.width - 16f, _topBarMoreMenuRect.height - 16f));
            for (int i = 0; overflowTabs != null && i < overflowTabs.Length; i++)
            {
                Rect buttonRect = GUILayoutUtility.GetRect(_topBarMoreMenuRect.width - 16f, 24f, GUILayout.Height(24f));
                DrawButton(buttonRect, overflowTabs[i], false);
                RegisterTopBarActionAliases(overflowTabs[i], buttonRect);
            }
            GUILayout.EndArea();
        }

        private Rect DrawTopBarWindowAction(Rect rect, ScenarioAuthoringShellViewModel shell)
        {
            for (int i = 0; shell.LayoutActions != null && i < shell.LayoutActions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = shell.LayoutActions[i];
                if (!IsWindowMenuAction(action))
                    continue;

                ScenarioAuthoringInspectorAction displayAction = _windowMenuOpen ? CloneEmphasized(action) : action;
                float width = Math.Max(104f, MeasureButtonWidth(action, false, 22f));
                Rect actionRect = rect.width <= width
                    ? rect
                    : new Rect(rect.xMax - width, rect.y, width, rect.height);
                DrawButton(actionRect, displayAction, false);
                RegisterTourTarget("action:" + ScenarioAuthoringActionIds.ActionShellOpenHelp, actionRect);
                RegisterTourTarget("action:" + ScenarioAuthoringActionIds.ActionShellOpenSettings, actionRect);
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
            float minimum = compact ? 58f : 76f;
            return Mathf.Max(minimum, MeasureButtonWidth(action, true, compact ? 18f : 30f));
        }

        private float ResolveChildStageTabWidth(ScenarioAuthoringInspectorAction action, bool compact)
        {
            return Mathf.Max(compact ? 62f : 86f, MeasureButtonWidth(action, true, compact ? 14f : 26f));
        }

        private float ResolveToolbarActionWidth(ScenarioAuthoringInspectorAction action, bool compact)
        {
            return Mathf.Max(compact ? 76f : 94f, MeasureButtonWidth(action, false, compact ? 16f : 24f));
        }

        private static bool IsLowPriorityTopBarAction(ScenarioAuthoringInspectorAction action)
        {
            return action != null
                && (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellOpenHelp, StringComparison.Ordinal)
                    || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionShellOpenSettings, StringComparison.Ordinal));
        }

        private static bool IsFinishStageTab(ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id))
                return false;

            return string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Test, StringComparison.Ordinal)
                || string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Publish, StringComparison.Ordinal);
        }

        private void RegisterTopBarActionAliases(ScenarioAuthoringInspectorAction action, Rect rect)
        {
            if (action == null || string.IsNullOrEmpty(action.Id))
                return;

            if (string.Equals(action.Id, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Events, StringComparison.Ordinal))
                RegisterTourTarget("action:" + ScenarioAuthoringActionIds.ActionShellOpenTimeline, rect);
        }

        private sealed class StageTabLayout
        {
            public ScenarioAuthoringInspectorAction Action;
            public bool Finish;
            public float Width;
        }

    }
}
