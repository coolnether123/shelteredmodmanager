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
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;
using ShelteredAPI.Scenarios.Presentation.UiKit.Theme;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;
using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private Rect DrawTopBarCore(Rect rect, ScenarioAuthoringShellViewModel shell, float openProgress)
        {
            Rect animatedRect = ResolveSlidingChromeRect(rect, openProgress, ScenarioUiSlideDirection.Up);
            Rect windowMenuButtonRect = default(Rect);
            using (ScenarioUiGuiScope.Apply(openProgress, animatedRect, 1f))
            {
            bool compact = IsCompactTopBar(animatedRect, shell);
            float primaryRowY = compact ? 34f : 8f;
            float primaryRowHeight = compact ? 28f : 32f;
            float utilityRowY = compact ? 6f : 46f;
            float utilityRowHeight = compact ? 26f : 28f;
            DrawChromePanel(animatedRect, _rootPanelStyle);
            DrawTopBarGoldRule(animatedRect);

            Rect brandRect = compact
                ? new Rect(animatedRect.x + 14f, animatedRect.y + 5f, 206f, 30f)
                : new Rect(animatedRect.x + 18f, animatedRect.y + 7f, 220f, 70f);
            GUI.Label(new Rect(brandRect.x, brandRect.y, brandRect.width, 30f), "SHELTERED", _titleStyle);
            if (!compact)
                GUI.Label(new Rect(brandRect.x, brandRect.y + 31f, brandRect.width, 20f), "Scenario Workshop", _smallTitleStyle);
            if (!compact && shell != null && !string.IsNullOrEmpty(shell.Subtitle))
                GUI.Label(new Rect(brandRect.x, brandRect.y + 52f, brandRect.width, 18f), ShortenToFit(shell.Subtitle, brandRect.width, _mutedTextStyle), _mutedTextStyle);

            float primaryRowLeft = compact ? animatedRect.x + 12f : brandRect.xMax + 20f;
            float actionRight = animatedRect.xMax - 10f;
            Rect loadingChipRect = DrawWorldLoadingChip(animatedRect, compact);
            if (loadingChipRect.width > 0f)
                actionRight = loadingChipRect.x - (compact ? 4f : 8f);
            windowMenuButtonRect = DrawTopBarWindowAction(
                compact
                    ? new Rect(Mathf.Max(primaryRowLeft, actionRight - 112f), animatedRect.y + 6f, Mathf.Min(112f, Math.Max(0f, actionRight - primaryRowLeft)), 26f)
                    : new Rect(primaryRowLeft, animatedRect.y + utilityRowY, actionRight - primaryRowLeft, utilityRowHeight),
                shell);

            Rect globalSearchButtonRect = DrawTopBarGlobalSearchButton(windowMenuButtonRect, animatedRect, compact);

            float saveWidth = MeasureTopBarActionsWidth(shell.ToolbarActions, compact);
            float saveRight = globalSearchButtonRect.width > 0f
                ? globalSearchButtonRect.x - (compact ? 4f : 8f)
                : (windowMenuButtonRect.width > 0f ? windowMenuButtonRect.x - (compact ? 4f : 8f) : actionRight);
            float saveX = Math.Max(primaryRowLeft, saveRight - saveWidth);
            Rect saveRect = new Rect(saveX, animatedRect.y + utilityRowY, Math.Max(0f, saveRight - saveX), utilityRowHeight);
            DrawTopBarToolbarActions(saveRect, shell, compact);

            float primaryTabsRight = compact ? animatedRect.xMax - 12f : Math.Max(primaryRowLeft, saveX - 12f);
            DrawMeasuredStageTabs(new Rect(primaryRowLeft, animatedRect.y + primaryRowY, Math.Max(0f, primaryTabsRight - primaryRowLeft), primaryRowHeight), shell, compact);

            if (_snapshot != null && IsWorldStage(_snapshot.State))
            {
                Rect worldControlsRect = compact
                    ? new Rect(primaryRowLeft, animatedRect.y + 60f, primaryTabsRight - primaryRowLeft, 24f)
                    : new Rect(primaryRowLeft, animatedRect.y + 50f, Math.Max(0f, saveX - primaryRowLeft - 12f), 26f);
                DrawWorldSurfaceControls(worldControlsRect, shell, compact);
            }

            }
            return windowMenuButtonRect;
        }

        // Lays twin rule lines along the bottom of the leather chrome band so it
        // reads like a book-cover strip sitting above the parchment pages: a
        // gold hairline over a darker shadow line.
        private void DrawTopBarGoldRule(Rect animatedRect)
        {
            if (_uiContext == null || _uiContext.Styles == null || animatedRect.width <= 24f)
                return;

            float x = animatedRect.x + 6f;
            float width = animatedRect.width - 12f;
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, animatedRect.yMax - 3f, width, 1f), _uiContext.Styles.BorderStrongTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(x, animatedRect.yMax - 2f, width, 1f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private Rect DrawWorldLoadingChip(Rect animatedRect, bool compact)
        {
            ScenarioAuthoringState state = _snapshot != null ? _snapshot.State : null;
            if (state == null || !state.WorldLoading)
                return RuntimeCompat.ZeroRect();

            string status = string.IsNullOrEmpty(state.WorldLoadingStatus) ? "Loading game" : state.WorldLoadingStatus;
            string label = compact ? "Loading game" : ShortenToFit(status, 260f, _statusStyle);
            float width = compact ? 132f : Mathf.Clamp(ScenarioUiMeasuredLabel.Width(label, _statusStyle, 28f), 148f, 280f);
            Rect rect = new Rect(animatedRect.xMax - width - 10f, animatedRect.y + (compact ? 36f : 8f), width, compact ? 24f : 28f);
            GUI.Box(rect, label, _statusStyle);
            return rect;
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

            float moreWidth = Mathf.Clamp(ScenarioUiMeasuredLabel.Width("More >", _buttonStyle, 28f), 82f, 112f);
            List<StageTabLayout> visibleTabs;
            List<StageTabLayout> overflowLayouts;
            ResolveStageTabOverflow(tabs, rect.width, moreWidth, out visibleTabs, out overflowLayouts);
            bool overflow = overflowLayouts.Count > 0;
            float availableRight = overflow ? rect.xMax - moreWidth - 4f : rect.xMax;
            float x = rect.x;
            bool drewMain = false;
            for (int i = 0; i < visibleTabs.Count; i++)
            {
                StageTabLayout tab = visibleTabs[i];
                if (tab.Finish && drewMain && x + 8f < availableRight)
                {
                    ScenarioUiWidgets.DrawVerticalDivider(new Rect(x + 2f, rect.y + 5f, 1f, rect.height - 10f), _uiContext.Styles);
                    x += 10f;
                }

                Rect tabRect = new Rect(x, rect.y, tab.Width, rect.height);
                if (tabRect.xMax > availableRight)
                    break;
                DrawButton(tabRect, tab.Action, true);
                if (tab.Action != null && tab.Action.Emphasized && _uiContext != null && _uiContext.Styles != null)
                    ScenarioUiAtlasSkin.DrawCornerCutBorder(tabRect, _uiContext.Styles.BorderStrongTexture, _uiContext.Styles.BorderSubtleTexture);
                RegisterTopBarActionAliases(tab.Action, tabRect);
                x = tabRect.xMax + 2f;
                if (!tab.Finish)
                    drewMain = true;
            }

            if (!overflow)
            {
                _topBarMoreMenuOpen = false;
                ScenarioAuthoringRendererInteractionState.Instance.TopBarMoreOpen = false;
                return;
            }

            List<ScenarioAuthoringInspectorAction> overflowTabs = new List<ScenarioAuthoringInspectorAction>();
            for (int i = 0; i < overflowLayouts.Count; i++)
                overflowTabs.Add(overflowLayouts[i].Action);
            _topBarOverflowTabs = overflowTabs.ToArray();

            _topBarMoreButtonRect = new Rect(rect.xMax - moreWidth, rect.y, moreWidth, rect.height);
            ScenarioAuthoringInspectorAction moreAction = new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionRendererTopBarMoreToggle,
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
                ScenarioAuthoringInspectorAction displayAction = compact ? BuildCompactStageTabAction(action) : action;

                result.Add(new StageTabLayout
                {
                    Action = displayAction,
                    Finish = IsFinishStageTab(action),
                    Width = ResolvePrimaryStageTabWidth(displayAction, compact)
                });
            }

            return result;
        }

        private static void ResolveStageTabOverflow(
            List<StageTabLayout> tabs,
            float availableWidth,
            float moreWidth,
            out List<StageTabLayout> visibleTabs,
            out List<StageTabLayout> overflowTabs)
        {
            visibleTabs = new List<StageTabLayout>();
            overflowTabs = new List<StageTabLayout>();
            if (tabs == null || tabs.Count == 0)
                return;

            bool[] overflow = new bool[tabs.Count];
            if (MeasureStageTabRunWidth(tabs, overflow) > availableWidth)
            {
                float limit = Math.Max(0f, availableWidth - moreWidth - 4f);
                for (int i = tabs.Count - 1; i >= 1 && MeasureStageTabRunWidth(tabs, overflow) > limit; i--)
                    overflow[i] = true;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                if (overflow[i])
                    overflowTabs.Add(tabs[i]);
                else
                    visibleTabs.Add(tabs[i]);
            }
        }

        private static float MeasureStageTabRunWidth(List<StageTabLayout> tabs, bool[] overflow)
        {
            float width = 0f;
            bool drewMain = false;
            bool first = true;
            for (int i = 0; i < tabs.Count; i++)
            {
                if (overflow != null && i < overflow.Length && overflow[i])
                    continue;

                StageTabLayout tab = tabs[i];
                if (!first)
                    width += 2f;
                if (tab.Finish && drewMain)
                    width += 10f;
                width += tab.Width;
                if (!tab.Finish)
                    drewMain = true;
                first = false;
            }

            return width;
        }

        private void DrawTopBarMoreButton(Rect rect, ScenarioAuthoringInspectorAction action)
        {
            if (DrawPlainButton(rect, new GUIContent(action.Label, action.Hint), _topBarMoreMenuOpen ? _activeButtonStyle : _buttonStyle, true))
            {
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionRendererTopBarMoreToggle);
                _topBarMoreMenuOpen = ScenarioAuthoringRendererInteractionState.Instance.TopBarMoreOpen;
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
            float height = 16f + ((overflowTabs != null ? overflowTabs.Length : 0) * 32f);
            _topBarMoreMenuRect = new Rect(anchor.xMax - width, anchor.yMax + 4f, width, height);
            GUI.Box(_topBarMoreMenuRect, GUIContent.none, _uiContext.Styles.Menu);
            GUILayout.BeginArea(new Rect(_topBarMoreMenuRect.x + 8f, _topBarMoreMenuRect.y + 8f, _topBarMoreMenuRect.width - 16f, _topBarMoreMenuRect.height - 16f));
            for (int i = 0; overflowTabs != null && i < overflowTabs.Length; i++)
            {
                Rect buttonRect = GUILayoutUtility.GetRect(_topBarMoreMenuRect.width - 16f, 32f, GUILayout.Height(32f));
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
            float minimum = compact ? 52f : 80f;
            return Mathf.Max(minimum, MeasureButtonWidth(action, true, compact ? 14f : 30f));
        }

        private static ScenarioAuthoringInspectorAction BuildCompactStageTabAction(ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return null;

            string label = action.Label;
            if (string.Equals(label, "Supplies", StringComparison.OrdinalIgnoreCase))
                label = "Supply";
            else if (string.Equals(label, "Timeline", StringComparison.OrdinalIgnoreCase))
                label = "Time";

            return string.Equals(label, action.Label, StringComparison.Ordinal)
                ? action
                : CloneWithLabel(action, label);
        }

        private float ResolveChildStageTabWidth(ScenarioAuthoringInspectorAction action, bool compact)
        {
            return Mathf.Max(compact ? 70f : 90f, MeasureButtonWidth(action, true, compact ? 28f : 30f));
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
